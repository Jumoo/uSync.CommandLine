using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

using Umbraco.Cms.Web.BackOffice.Security;

using uSync.Commands.Server.Configuration;

namespace uSync.Platform.Command.Server.Auth;
internal class SyncCommandAuthenticationHandler
    : AuthenticationHandler<SyncCommandAuthenticationOptions>
{
    private const string c_authorizationHeader = "Authorization";

    private readonly ILogger<SyncCommandAuthenticationHandler> _logger;
    private readonly IOptionsMonitor<SyncCommandConfiguration> _commandConfig;

    private readonly IBackOfficeSignInManager _backOfficeSignInManager;
    private readonly IBackOfficeUserManager _backOfficeUserManager;

    public SyncCommandAuthenticationHandler(
        IOptionsMonitor<SyncCommandConfiguration> commandConfig,
        IOptionsMonitor<SyncCommandAuthenticationOptions> options,
        ILoggerFactory logger,
        IBackOfficeSignInManager backOfficeSignInManager,
        UrlEncoder urlEncoder,
        ISystemClock clock,
        IBackOfficeUserManager backOfficeUserManager)
        : base(options, logger, urlEncoder, clock)
    {
        _logger = logger.CreateLogger<SyncCommandAuthenticationHandler>();
        _commandConfig = commandConfig;

        _backOfficeSignInManager = backOfficeSignInManager;
        _backOfficeUserManager = backOfficeUserManager;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = await ValidateToUser();
        if (user != null)
        {
            var ticket = await GetUmbracoAuthTicket(user);
            return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.Fail(new AccessViolationException("Invalid authentication"));
    }

    private async Task<BackOfficeIdentityUser?> ValidateToUser()
    {
        if (string.IsNullOrWhiteSpace(_commandConfig.CurrentValue.Enabled))
            return null;

        var headerContent = Request.Headers[c_authorizationHeader].SingleOrDefault();
        if (headerContent == null) return null;

        var authHeader = headerContent.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (authHeader.Length != 2) return null;

        var scheme = authHeader[0];

        switch (scheme)
        {
            case SyncCommandAuthenticationOptions.DefaultScheme:
                return await ValidateHmacAuth(authHeader[1]);
            case "Basic":
                return await ValidateBasicAuth(authHeader[1]);
            default:
                return null;
        }
    }

    public async Task<BackOfficeIdentityUser?> ValidateBasicAuth(string authHeaderValue)
    {
        if (!_commandConfig.CurrentValue.Enabled.Contains("basic", StringComparison.OrdinalIgnoreCase))
            return null;

        var (username, password) = GetUsernameAndPassword(authHeaderValue);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return null;

        return await ValidateUmbracoUser(username, password);
    }

    public async Task<BackOfficeIdentityUser?> ValidateHmacAuth(string authHeaderValue)
    {
        if (!_commandConfig.CurrentValue.Enabled.Contains("HMAC", StringComparison.OrdinalIgnoreCase))
            return null;

        var hmackey = _commandConfig.CurrentValue.Key;
        if (string.IsNullOrWhiteSpace(hmackey)) return null;

        var parameters = GetHmacAuthHeader(authHeaderValue);
        if (parameters == null) return null;

        // stop multiple requests with same nonce value
        if (MemoryCache.Default.Contains(parameters.Nonce)) return null;

        var timestampTime = DateTimeOffset.FromUnixTimeSeconds(parameters.Timestamp);
        if ((DateTime.UtcNow - timestampTime).TotalSeconds > Options.AllowedDateDrift.TotalSeconds)
            return null;

        // if the signature is OK, 
        if (CheckSigniture(hmackey, parameters, Request)) return null;

        return await _backOfficeUserManager.FindByIdAsync(Constants.Security.SuperUserIdAsString);
    }

    private HmacParameters? GetHmacAuthHeader(string authHeader)
    {
        var array = authHeader.Split(":");
        if (array.Length != 3) return null;

        return new HmacParameters
        {
            Signature = array[0],
            Nonce = array[1],
            Timestamp = Convert.ToInt64(array[2])
        };
    }

    private bool CheckSigniture(string key, HmacParameters parameters, HttpRequest request)
    {
        var token = $"{request.Method}" +
            $"{parameters.Timestamp}" +
            $"{parameters.Nonce}" +
            $"{request.ContentLength ?? 0}";

        var secretBytes = Convert.FromBase64String(key);
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        using (HMACSHA256 hmac = new HMACSHA256(secretBytes))
        {
            var hashed = hmac.ComputeHash(tokenBytes);
            string stringToken = Convert.ToBase64String(hashed);

            var match = parameters.Signature?.Equals(stringToken, StringComparison.Ordinal) ?? false;
            if (match)
                MemoryCache.Default.Add(parameters.Nonce, parameters.Timestamp, DateTime.Now.AddMinutes(180));

            return match;
        }
    }

    private async Task<AuthenticationTicket> GetUmbracoAuthTicket(BackOfficeIdentityUser user)
    {
        if (user != null)
        {
            var principle = await _backOfficeSignInManager.CreateUserPrincipalAsync(user);
            var ticket = new AuthenticationTicket(principle, Scheme.Name);
            return ticket;
        }

        throw new Exception("Unable to generate user ticket");
    }


    private (string? username, string? password) GetUsernameAndPassword(string authParameter)
    {
        byte[] credentialBytes;

        try
        {
            credentialBytes = Convert.FromBase64String(authParameter);
        }
        catch (FormatException)
        {
            return (null, null);
        }

        // The currently approved HTTP 1.1 specification says characters here are ISO-8859-1.
        // However, the current draft updated specification for HTTP 1.1 indicates this encoding is infrequently
        // used in practice and defines behavior only for ASCII.
        Encoding encoding = Encoding.ASCII;
        // Make a writable copy of the encoding to enable setting a decoder fall back.
        encoding = (Encoding)encoding.Clone();
        // Fail on invalid bytes rather than silently replacing and continuing.
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        string decodedCredentials;

        try
        {
            decodedCredentials = encoding.GetString(credentialBytes);
        }
        catch (DecoderFallbackException)
        {
            return (null, null);
        }

        if (String.IsNullOrEmpty(decodedCredentials))
        {
            return (null, null);
        }

        int colonIndex = decodedCredentials.IndexOf(':');

        if (colonIndex == -1)
        {
            return (null, null);
        }

        string userName = decodedCredentials.Substring(0, colonIndex);
        string password = decodedCredentials.Substring(colonIndex + 1);
        return (userName, password);
    }


    private async Task<BackOfficeIdentityUser?> ValidateUmbracoUser(string username, string password)
    {
        try
        {
            var user = await _backOfficeUserManager.FindByNameAsync(username);
            if (user == null || !user.IsApproved || user.IsLockedOut) return null;

            if (await _backOfficeUserManager.CheckPasswordAsync(user, password))
                return user;
        }
        catch
        {
            return null;
        }

        return null;

    }
}

internal class SyncCommandAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "hmacauth";
    public string Scheme => DefaultScheme;
    public TimeSpan AllowedDateDrift => TimeSpan.FromSeconds(120);
    public Func<string, string[]>? GetRolesForId { get; set; }
}

internal class HmacParameters
{
    public string? Signature { get; set; }
    public string? Nonce { get; set; }
    public long Timestamp { get; set; }
}