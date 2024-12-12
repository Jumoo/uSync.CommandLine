using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.Runtime.CompilerServices;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Models;

public class ModelsRebuildCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }
    public ModelsRebuildCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("models-rebuild", "Rebuild the models on the server");
        AddCoreOptions(Command);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            await umbracoClient.PostModelsBuilderBuildAsync();
            await writer.WriteLineAsync("Models Rebuild Requested");
            context.ExitCode = 0;
        });
    }
}
