using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

namespace uSync.Commands.Core.Commands;

public abstract class SyncCommandBase
{
    protected readonly TextWriter Writer;
    protected readonly IConfiguration Configuration;

    public SyncCommandBase(TextWriter writer, IConfiguration configuration)
    {
        Writer = writer;
        Configuration = configuration;
    }

    /// <summary>
    ///  environment variables for the connection, so pipelines don't need
    ///  the client secret on the command line (where it can end up in logs).
    /// </summary>
    public const string ServerEnvironmentVariable = "USYNC_SERVER";
    public const string ClientIdEnvironmentVariable = "USYNC_CLIENT_ID";
    public const string ClientSecretEnvironmentVariable = "USYNC_CLIENT_SECRET";

    /// <summary>
    ///  reads environment variables (swappable for tests).
    /// </summary>
    public Func<string, string?> GetEnvironmentVariable { get; set; } = Environment.GetEnvironmentVariable;

    protected Option<Uri> optionServerUrl = new Option<Uri>(new string[] { "--server-url", "-s" }, $"The URL of the uSync server (or set {ServerEnvironmentVariable})");
    protected Option<string> optionSecret = new Option<string>(new string[] { "--secret", "-k" }, $"The secret key for the uSync server (or set {ClientSecretEnvironmentVariable})");
    protected Option<string> optionClientId = new Option<string>(new string[] { "--client-id", "-i" }, $"The client ID for the uSync server (or set {ClientIdEnvironmentVariable})");

    protected void AddCoreOptions(Command command)
    {
        command.AddOption(optionServerUrl);
        command.AddOption(optionSecret);
        command.AddOption(optionClientId);
    }

    public SyncConnectionParameters GetConnectionPartameters(InvocationContext context)
    {
        // command line, then environment variables, then appsettings.
        var clientId = context.ParseResult?.GetValueForOption(optionClientId)
            ?? FromEnvironment(ClientIdEnvironmentVariable)
            ?? Configuration["uSync:Command:ClientId"]
            ?? throw new Exception($"No client id: use --client-id, set {ClientIdEnvironmentVariable}, or uSync:Command:ClientId in appsettings.json");
        if (!clientId.StartsWith("umbraco-back-office"))
        {
            throw new Exception("ClientId does not start with 'umbraco-back-office'");
        }
        return new SyncConnectionParameters
        {
            Url = GetServerUri(context) ?? throw new Exception($"No server: use --server-url, set {ServerEnvironmentVariable}, or uSync:Command:ServerUrl in appsettings.json"),
            ClientSecret = context.ParseResult?.GetValueForOption(optionSecret)
                ?? FromEnvironment(ClientSecretEnvironmentVariable)
                ?? Configuration["uSync:Command:Secret"]
                ?? throw new Exception($"No client secret: use --secret, set {ClientSecretEnvironmentVariable}, or uSync:Command:Secret in appsettings.json"),
            ClientId = clientId
        };
    }

    private string? FromEnvironment(string name)
    {
        var value = GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Uri? GetServerUri(InvocationContext context)
    {
        var uri = context.ParseResult?.GetValueForOption(optionServerUrl);
        if (uri is not null) return uri;

        var fromEnvironment = FromEnvironment(ServerEnvironmentVariable);
        if (fromEnvironment is not null)
        {
            // the api paths are added straight on to the url, so it needs to end with a /
            if (fromEnvironment.EndsWith('/') is false) fromEnvironment += "/";
            if (Uri.TryCreate(fromEnvironment, UriKind.Absolute, out var environmentUri)) return environmentUri;
            throw new Exception($"{ServerEnvironmentVariable} is not a valid url");
        }

        uri = GetServerUriFromConfig("uSync:Command:ServerUrl");
        if (uri is not null) return uri;

        uri = GetServerUriFromConfig("Umbraco:CMS:WebRouting:UmbracoApplicationUrl");
        if (uri is not null) return uri;

        uri = GetServerUrlFromLaunchSettings("Properties/launchSettings.json");
        if (uri is not null) return uri;

#if DEBUG
        uri = GetServerUrlFromLaunchSettings("../Umbraco.Site/Properties/launchSettings.json");
        if (uri is not null) return uri;
#endif

        return null;
    }

    private Uri? GetServerUriFromConfig(string configPath)
    {
        var urlString = Configuration[configPath];
        if (string.IsNullOrWhiteSpace(urlString)) return null;

        if (Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
            return uri;

        return null;
    }

    private Uri? GetServerUrlFromLaunchSettings(string filePath)
    {
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
        if (!File.Exists(fullPath)) return null;

        var json = File.ReadAllText(fullPath);
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("profiles", out var profilesElement) &&
            profilesElement.TryGetProperty("Umbraco.Web.UI", out var umbracoElement) &&
            umbracoElement.TryGetProperty("applicationUrl", out var applicationUrlElement))
        {
            var urlString = applicationUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(urlString)) return null;

            if (Uri.TryCreate(urlString.Split(';')[0], UriKind.Absolute, out var uri))
                return uri;
        }

        return null;
    }
}

public class SyncConnectionParameters
{
    public required Uri Url { get; set; }
    public required string ClientSecret { get; set; }
    public required string ClientId { get; set; }
}