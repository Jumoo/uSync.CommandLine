using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.CommandLine.Invocation;

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
            Url = context.ParseResult?.GetValueForOption(optionServerUrl) ?? throw new Exception("No host"),
            ClientSecret = context.ParseResult?.GetValueForOption(optionSecret) ?? Configuration["uSync:Command:Secret"] ?? throw new Exception("No Client Secret"),
            ClientId = context.ParseResult?.GetValueForOption(optionClientId) ?? Configuration["uSync:Command:ClientId"] ?? throw new Exception("No ClientId")
        };    
    }
}

public class SyncConnectionParameters
{
    public required Uri Url { get; set; }
    public required string ClientSecret { get; set; }
    public required string ClientId { get; set; }
}