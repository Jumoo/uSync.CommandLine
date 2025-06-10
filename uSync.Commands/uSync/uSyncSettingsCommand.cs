using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.uSync;

public class uSyncSettingsCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public uSyncSettingsCommand(
        TextWriter wrtier,
        IConfiguration configuration,
        HttpClient client) : base(wrtier, configuration, client)
    {
        Command = new Command("usync-settings", "List all settings");
        AddCoreOptions(Command);
        Command.SetHandler(async (context) =>
        {
            var token = await GetToken(context);
            var uSyncClient = await GetUSyncClient(context, token);
            var settings = await uSyncClient.GetSettingsAsync();

            await wrtier.WriteAsync($"uSync Settings: {settings.Folders.Count} folders");

            context.ExitCode = 0;
        });
    }
}
