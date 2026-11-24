using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.uSync;

public class uSyncSettingsCommand : ConnectedCommandBase, ISyncCommand
{
    private readonly Option<bool> _json = JsonOutput.CreateOption();

    public Command Command { get; }

    public uSyncSettingsCommand(
        TextWriter wrtier,
        IConfiguration configuration,
        HttpClient client) : base(wrtier, configuration, client)
    {
        Command = new Command("usync-settings", "List all settings");
        AddCoreOptions(Command);
        Command.AddOption(_json);
        Command.SetHandler(async (context) =>
        {
            var token = await GetToken(context);
            var uSyncClient = await GetUSyncClient(context, token);
            var settings = await uSyncClient.GetSettingsAsync();

            if (context.ParseResult.GetValueForOption(_json))
            {
                await JsonOutput.WriteAsync(Writer, settings);
                context.ExitCode = 0;
                return;
            }

            await wrtier.WriteAsync($"uSync Settings: {settings.Folders.Count} folders");

            context.ExitCode = 0;
        });
    }
}
