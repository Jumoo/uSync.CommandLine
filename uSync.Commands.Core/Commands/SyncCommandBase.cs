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
        var connection = DescribeConnection(context);

        var clientId = connection.ClientId.Value
            ?? throw new SyncUsageException($"No client id: use --client-id, set {ClientIdEnvironmentVariable}, or uSync:Command:ClientId in appsettings.json");
        if (!clientId.StartsWith("umbraco-back-office"))
        {
            throw new SyncUsageException($"The client id '{clientId}' does not start with 'umbraco-back-office' - API user client ids in Umbraco always do.");
        }
        return new SyncConnectionParameters
        {
            Url = connection.ServerUrl ?? throw new SyncUsageException($"No server: use --server-url, set {ServerEnvironmentVariable}, or uSync:Command:ServerUrl in appsettings.json"),
            ClientSecret = connection.Secret.Value
                ?? throw new SyncUsageException($"No client secret: use --secret, set {ClientSecretEnvironmentVariable}, or uSync:Command:Secret in appsettings.json"),
            ClientId = clientId
        };
    }

    /// <summary>
    ///  each connection setting, and where it came from - without failing when one is missing.
    /// </summary>
    public SyncConnectionDescription DescribeConnection(InvocationContext context)
    {
        var url = GetServerUri(context, out var serverSource);
        return new SyncConnectionDescription
        {
            ServerUrl = url,
            Server = new SyncConnectionSetting(url?.ToString(), serverSource),
            ClientId = Resolve(context, optionClientId, "--client-id", ClientIdEnvironmentVariable, "uSync:Command:ClientId"),
            Secret = Resolve(context, optionSecret, "--secret", ClientSecretEnvironmentVariable, "uSync:Command:Secret"),
        };
    }

    private SyncConnectionSetting Resolve(InvocationContext context, Option<string> option, string optionName, string environmentVariable, string configKey)
    {
        var value = context.ParseResult?.GetValueForOption(option);
        if (value is not null) return new(value, optionName);

        value = FromEnvironment(environmentVariable);
        if (value is not null) return new(value, environmentVariable);

        value = Configuration[configKey];
        if (value is not null) return new(value, $"appsettings.json {configKey}");

        return new(null, null);
    }

    private string? FromEnvironment(string name)
    {
        var value = GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Uri? GetServerUri(InvocationContext context, out string? source)
    {
        source = "--server-url";
        var uri = context.ParseResult?.GetValueForOption(optionServerUrl);
        if (uri is not null) return uri;

        source = ServerEnvironmentVariable;
        var fromEnvironment = FromEnvironment(ServerEnvironmentVariable);
        if (fromEnvironment is not null)
        {
            // the api paths are added straight on to the url, so it needs to end with a /
            if (fromEnvironment.EndsWith('/') is false) fromEnvironment += "/";
            if (Uri.TryCreate(fromEnvironment, UriKind.Absolute, out var environmentUri)) return environmentUri;
            throw new SyncUsageException($"{ServerEnvironmentVariable} is not a valid url");
        }

        source = "appsettings.json uSync:Command:ServerUrl";
        uri = GetServerUriFromConfig("uSync:Command:ServerUrl");
        if (uri is not null) return uri;

        source = "appsettings.json Umbraco:CMS:WebRouting:UmbracoApplicationUrl";
        uri = GetServerUriFromConfig("Umbraco:CMS:WebRouting:UmbracoApplicationUrl");
        if (uri is not null) return uri;

        source = "Properties/launchSettings.json";
        uri = GetServerUrlFromLaunchSettings("Properties/launchSettings.json");
        if (uri is not null) return uri;

#if DEBUG
        source = "../Umbraco.Site/Properties/launchSettings.json";
        uri = GetServerUrlFromLaunchSettings("../Umbraco.Site/Properties/launchSettings.json");
        if (uri is not null) return uri;
#endif

        source = null;
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

/// <summary>
///  a connection setting's value and where it came from (null when it isn't set).
/// </summary>
public sealed record SyncConnectionSetting(string? Value, string? Source);

public class SyncConnectionDescription
{
    public Uri? ServerUrl { get; init; }
    public required SyncConnectionSetting Server { get; init; }
    public required SyncConnectionSetting ClientId { get; init; }
    public required SyncConnectionSetting Secret { get; init; }
}
