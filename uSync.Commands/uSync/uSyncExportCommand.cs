using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Management.Api;

namespace uSync.Commands.uSync;

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
            // do the actual work on the thing. 
            var results = await Process(context, request);
            // do the results.
            await DisplayResults("Exported", results);
            context.ExitCode = 0;
        });
    }
}
