using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Models;

public class ModelsStatusCommand: ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }
    public ModelsStatusCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("models-status", "Get the status of the models on the server");
        AddCoreOptions(Command);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var status = await umbracoClient.GetModelsBuilderStatusAsync();
            await writer.WriteLineAsync($"Models Status: {status.Status}");
            context.ExitCode = 0;
        });
    }
}