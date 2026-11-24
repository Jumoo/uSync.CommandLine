using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.Models;

public class ModelsStatusCommand: ConnectedCommandBase, ISyncCommand
{
    private readonly Option<bool> _json = JsonOutput.CreateOption();

    public Command Command { get; }
    public ModelsStatusCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("models-status", "Get the status of the models on the server");
        AddCoreOptions(Command);
        Command.AddOption(_json);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var status = await umbracoClient.GetModelsBuilderStatusAsync();

            if (context.ParseResult.GetValueForOption(_json))
            {
                await JsonOutput.WriteAsync(Writer, status);
                context.ExitCode = 0;
                return;
            }
            await writer.WriteLineAsync($"Models Status: {status.Status}");
            context.ExitCode = 0;
        });
    }
}