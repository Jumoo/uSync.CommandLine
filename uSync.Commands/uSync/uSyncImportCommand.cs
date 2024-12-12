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
            var set = context.ParseResult.GetValueForOption(this.set) ?? "default";
            var group = context.ParseResult.GetValueForOption(this.group) ?? "all";

            var request = new PerformActionRequest
            {
                Action = "Import",
                Options = new USyncOptions
                {
                    Set = set,
                    Group = group,
                    Force = force,
                },
                RequestId = Guid.NewGuid().ToString(),
            };

            // do the actuall work on the thing. 
            var results = await Process(context, request);

            // do the results.
            await DisplayResults(results);
            context.ExitCode = 0;
        });
    }
}

public class uSyncExportCommand : uSyncPerformCommandBase, ISyncCommand
{
    public Command Command { get; }

    public uSyncExportCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("usync-export", "Export all items") { set, group };
        AddCoreOptions(Command);
        Command.SetHandler(async (context) =>
        {
            var set = context.ParseResult.GetValueForOption(this.set) ?? "default";
            var group = context.ParseResult.GetValueForOption(this.group) ?? "all";
            var request = new PerformActionRequest
            {
                Action = "Export",
                Options = new USyncOptions
                {
                    Set = set,
                    Group = group,
                },
                RequestId = Guid.NewGuid().ToString(),
            };
            // do the actuall work on the thing. 
            var results = await Process(context, request);
            // do the results.
            await DisplayResults(results);
            context.ExitCode = 0;
        });
    }
}
