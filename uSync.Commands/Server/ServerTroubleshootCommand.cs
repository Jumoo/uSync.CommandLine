using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.Server;

/// <summary>
///  the site's troubleshooting information (versions, OS, database...),
///  the same as the backoffice's "Information" dashboard - ready to paste into an issue.
/// </summary>
public class ServerTroubleshootCommand : ConnectedCommandBase, ISyncCommand
{
    private readonly Option<bool> _markdown = new("--markdown",
        "write a markdown table (for pasting into an issue)");

    public Command Command { get; }

    public ServerTroubleshootCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("server-troubleshoot", "Show the site's troubleshooting information (versions, OS, database), for support issues")
        {
            _markdown
        };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var information = await umbracoClient.GetServerTroubleshootingAsync(context.GetCancellationToken());
            var items = information.Items.Select(x => (x.Name, x.Data)).ToList();

            await Writer.WriteAsync(context.ParseResult.GetValueForOption(_markdown)
                ? ToMarkdown(items)
                : ToText(items));

            context.ExitCode = 0;
        });
    }

    public static string ToText(IReadOnlyList<(string Name, string Data)> items)
    {
        var width = items.Count == 0 ? 0 : items.Max(x => x.Name?.Length ?? 0);
        return string.Concat(items.Select(x => $"{(x.Name ?? string.Empty).PadRight(width)}  {x.Data}{Environment.NewLine}"));
    }

    public static string ToMarkdown(IReadOnlyList<(string Name, string Data)> items)
        => $"| | |{Environment.NewLine}|---|---|{Environment.NewLine}"
            + string.Concat(items.Select(x => $"| {StepSummary.Cell(x.Name ?? string.Empty)} | {StepSummary.Cell(x.Data ?? string.Empty)} |{Environment.NewLine}"));
}
