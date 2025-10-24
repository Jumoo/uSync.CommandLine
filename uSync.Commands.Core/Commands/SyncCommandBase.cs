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

    protected Option<Uri> optionServerUrl = new Option<Uri>(new string[] { "--server-url", "-s" }, "The URL of the uSync server");
    protected Option<string> optionSecret = new Option<string>(new string[] { "--secret", "-k" }, "The secret key for the uSync server");
    protected Option<string> optionClientId = new Option<string>(new string[] { "--client-id", "-i" }, "The client ID for the uSync server");

    protected void AddCoreOptions(Command command)
    {
        command.AddOption(optionServerUrl);
        command.AddOption(optionSecret);
        command.AddOption(optionClientId);
    }

    public SyncConnectionParameters GetConnectionPartameters(InvocationContext context)
    {
        return new SyncConnectionParameters
        {
            Url = GetServerUri(context) ?? throw new Exception("No host"),
            ClientSecret = context.ParseResult?.GetValueForOption(optionSecret) ?? Configuration["uSync:Command:Secret"] ?? throw new Exception("No Client Secret"),
            ClientId = context.ParseResult?.GetValueForOption(optionClientId) ?? Configuration["uSync:Command:ClientId"] ?? throw new Exception("No ClientId")
        };
    }

    private Uri? GetServerUri(InvocationContext context)
    {
        var uri = context.ParseResult?.GetValueForOption(optionServerUrl);
        if (uri is not null) return uri;

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
        if (urlString is not null) return new Uri(urlString);
        return null;
    }

    private Uri? GetServerUrlFromLaunchSettings(string filePath)
    {
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
        if (!File.Exists(fullPath)) return null;

        var json = File.ReadAllText(fullPath);
        var doc = JsonDocument.Parse(json);
        var urlString = doc.RootElement.GetProperty("profiles").GetProperty("Umbraco.Web.UI").GetProperty("applicationUrl").GetString();
        if (urlString is not null) 
            return new Uri(urlString.Split(';')[0]);
        
        return null;
    }
}

public class SyncConnectionParameters
{
    public required Uri Url { get; set; }
    public required string ClientSecret { get; set; }
    public required string ClientId { get; set; }
}