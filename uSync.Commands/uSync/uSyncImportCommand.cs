using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Management.Api;

namespace uSync.Commands.uSync;

public class uSyncImportCommand : uSyncPerformCommandBase, ISyncCommand
{
    protected Option<bool> force = new Option<bool>(["--force", "-f" ], "force the import");

    public Command Command { get; }
    public uSyncImportCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("usync-import", "Import all items") 
            { force, set,group  };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var force = context.ParseResult.GetValueForOption(this.force);
            var request = CreateRequest(context, "Import", force);

            // do the actual work on the thing.
            var results = await Process(context, request);

            // do the results.
            await DisplayResults("Imported", results);
            context.ExitCode = GetExitCode(results);
        });
    }
}
