using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.Text;
using System.Text.Json;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;
using uSync.Files;

namespace uSync.Commands.Core.Files;

/// <summary>
///  the uSync items a branch adds, changes or deletes - the list `files-verify --summary`
///  writes, without running the checks, as text, json or markdown. Offline, needs git.
/// </summary>
public class FilesDiffCommand : SyncCommandBase, ISyncCommand
{
    public static readonly string[] Formats = ["text", "json", "markdown"];

    private readonly Argument<string> _ref = new("ref",
        "git ref to compare with (e.g. origin/main): changes on this branch since then, plus uncommitted ones");

    private readonly Option<string> _format = new Option<string>("--format", () => "text", "output format")
        .FromAmong(Formats);

    private readonly Option<string?> _folder = new("--folder",
        "uSync folder (default: from appsettings.json uSync:Settings:RootFolder, else the highest uSync/v{n} folder)");

    public Command Command { get; }

    public FilesDiffCommand(TextWriter writer, IConfiguration configuration)
        : base(writer, configuration)
    {
        Command = new Command("files-diff", "List the uSync items added, changed or deleted since a git ref. Offline, needs git.")
        {
            _ref, _format, _folder
        };

        Command.SetHandler(context =>
        {
            var result = context.ParseResult;
            context.ExitCode = FilesCommandHelper.Run(() =>
            {
                var gitRef = result.GetValueForArgument(_ref);
                var location = FilesCommandHelper.Locate(result.GetValueForOption(_folder));
                var changes = GitChanges.GetChanges(location.Folder, gitRef);
                var items = FilesVerifySummary.GetItemChanges(location.Folder, changes, gitRef);

                Writer.Write(Format(result.GetValueForOption(_format) ?? "text", gitRef, items));
                return 0;
            });
        });
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string Format(string format, string gitRef, IReadOnlyList<SyncItemChange> items)
    {
        switch (format)
        {
            case "json":
                return JsonSerializer.Serialize(items.Select(x => new { type = x.Type, alias = x.Alias, change = x.Change, path = x.RelativePath }), JsonOptions)
                    + Environment.NewLine;

            case "markdown":
                var md = new StringBuilder();
                md.AppendLine($"#### uSync items changed since `{StepSummary.Cell(gitRef)}`");
                md.AppendLine();
                if (items.Count == 0)
                {
                    md.AppendLine("No uSync items changed.");
                    return md.ToString();
                }
                md.AppendLine("| Type | Alias | Change |");
                md.AppendLine("|---|---|---|");
                foreach (var item in items)
                    md.AppendLine($"| {StepSummary.Cell(item.Type)} | {StepSummary.Cell(item.Alias)} | {item.Change} |");
                return md.ToString();

            default:
                var text = new StringBuilder();
                if (items.Count == 0)
                {
                    text.AppendLine($"No uSync items changed since {gitRef}.");
                    return text.ToString();
                }
                text.AppendLine($"{items.Count} uSync item(s) changed since {gitRef}:");
                foreach (var type in items.GroupBy(x => x.Type))
                {
                    text.AppendLine($"  {type.Key}");
                    foreach (var item in type)
                        text.AppendLine($"    {item.Change,-8} {item.Alias}");
                }
                return text.ToString();
        }
    }
}
