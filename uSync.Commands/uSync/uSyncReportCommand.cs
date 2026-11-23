using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.uSync;

/// <summary>
///  dry run of an import: what would change on the site.
/// </summary>
/// <remarks>
///  uses the same perform action endpoint as import, with the "Report" action.
///  Exits 0 even when there are changes; 1 only when the report itself fails.
/// </remarks>
public class uSyncReportCommand : uSyncPerformCommandBase, ISyncCommand
{
    public Command Command { get; }

    public uSyncReportCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("usync-report", "Report what an import would change (dry run)")
            { set, group };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var request = CreateRequest(context, "Report");

            var results = await Process(context, request);

            if (results is not null)
            {
                var changes = results.Status?.Sum(x => x.Changes) ?? 0;
                await Writer.WriteLineAsync($"Report : {changes} change(s) would be made");
                await DisplayChangedItems(results);
            }

            WriteStepSummary("Report", results, GetConnectionPartameters(context).Url.ToString());

            context.ExitCode = results is null ? 1 : 0;
        });
    }
}
