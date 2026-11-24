using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Net.Http.Headers;

namespace uSync.Notify;

/// <summary>
///  swaps a "uSync-Notify-Token: {name}" header on an outgoing webhook for
///  "Authorization: Bearer {secret}", reading the secret from config.
/// </summary>
/// <remarks>
///  webhook headers are stored in the database and in the uSync files, so
///  a token can't go in them. This keeps the token in the site's environment
///  and the webhook just names it.
///
///  Umbraco logs the request headers after sending, from the request it built,
///  so the token is added to a copy and the log only shows the name.
/// </remarks>
internal class NotifyTokenHandler : DelegatingHandler
{
    public const string HeaderName = "uSync-Notify-Token";

    private readonly IOptionsMonitor<NotifyOptions> _options;
    private readonly ILogger<NotifyTokenHandler> _logger;

    public NotifyTokenHandler(IOptionsMonitor<NotifyOptions> options, ILogger<NotifyTokenHandler> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.TryGetValues(HeaderName, out var values) is false)
            return await base.SendAsync(request, cancellationToken);

        var name = values.FirstOrDefault()?.Trim() ?? string.Empty;
        var options = _options.CurrentValue;

        if (IsAllowedHost(request.RequestUri, options.AllowedHosts) is false)
        {
            _logger.LogWarning("Not sending the {name} token to {host}: it isn't in uSync:Notify:AllowedHosts", name, request.RequestUri?.Host);
            return Refuse(request, $"{request.RequestUri?.Host} is not in uSync:Notify:AllowedHosts");
        }

        if (options.Secrets.TryGetValue(name, out var secret) is false || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("No secret called {name} in uSync:Notify:Secrets (set uSync__Notify__Secrets__{name})", name, name);
            return Refuse(request, $"no secret called {name} in uSync:Notify:Secrets");
        }

        using var copy = CopyWithToken(request, secret.Trim());
        try
        {
            return await base.SendAsync(copy, cancellationToken);
        }
        finally
        {
            // the content belongs to the original request, Umbraco reads it again for the log.
            copy.Content = null;
        }
    }

    internal static bool IsAllowedHost(Uri? uri, IEnumerable<string> allowedHosts)
        => uri is not null
            && uri.Scheme == Uri.UriSchemeHttps
            && allowedHosts.Any(x => string.Equals(x.Trim(), uri.Host, StringComparison.OrdinalIgnoreCase));

    internal static HttpRequestMessage CopyWithToken(HttpRequestMessage request, string secret)
    {
        var copy = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
            Content = request.Content,
        };

        foreach (var header in request.Headers)
        {
            if (header.Key.Equals(HeaderName, StringComparison.OrdinalIgnoreCase)) continue;
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) continue;
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        copy.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return copy;
    }

    /// <summary>
    ///  fail the request without sending it, so Umbraco's webhook log shows why.
    /// </summary>
    private static HttpResponseMessage Refuse(HttpRequestMessage request, string reason)
        => new(System.Net.HttpStatusCode.Forbidden)
        {
            RequestMessage = request,
            ReasonPhrase = "uSync.Command.Notify: " + reason,
        };
}
