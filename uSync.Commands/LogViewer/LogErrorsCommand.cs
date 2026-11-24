using Microsoft.Extensions.Configuration;

using System.CommandLine;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.LogViewer;

/// <summary>
///  list the errors (or warnings) the site logged recently, and fail if there are any.
/// </summary>
/// <remarks>
///  run after an import as a smoke test. Reads Umbraco's log viewer, so it only
///  sees the logs of the server that answers the request.
/// </remarks>
public class LogErrorsCommand : ConnectedCommandBase, ISyncCommand
{
    public const string DefaultSince = "15m";
    public const int DefaultTake = 20;

    private readonly Option<string> _since = new("--since",
        () => DefaultSince, "how far back to look: a duration (90s, 15m, 2h, 1d) or a date/time");

    private readonly Option<string> _level = new Option<string>("--level",
        () => nameof(LogLevelModel.Error), "the lowest level to report")
        .FromAmong(nameof(LogLevelModel.Warning), nameof(LogLevelModel.Error), nameof(LogLevelModel.Fatal));

    private readonly Option<int> _take = new("--take",
        () => DefaultTake, "the most entries to list (all of them are counted)");

    private readonly Option<bool> _noFail = new("--no-fail",
        "exit 0 even when entries are found");

    public Command Command { get; }

    public LogErrorsCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("log-errors", "List errors logged on the site recently. Exits 1 if there are any.")
        {
            _since, _level, _take, _noFail
        };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var result = context.ParseResult;
            var sinceValue = result.GetValueForOption(_since);
            if (LogSince.TryParse(sinceValue, DateTimeOffset.Now, out var since) is false)
                throw new SyncUsageException($"--since '{sinceValue}' is not a duration (90s, 15m, 2h, 1d) or a date.");

            var lowest = Enum.Parse<LogLevelModel>(result.GetValueForOption(_level) ?? nameof(LogLevelModel.Error), true);
            var levels = LevelsFrom(lowest);
            var take = Math.Max(0, result.GetValueForOption(_take));

            var umbracoClient = await GetUmbracoClient(context);
            var now = DateTimeOffset.Now;

            // the generated client joins a logLevel array into one value ("Error,Fatal"),
            // which Umbraco rejects, so ask for each level on its own.
            long total = 0;
            var entries = new List<LogMessageResponseModel>();
            foreach (var level in levels)
            {
                var logs = await umbracoClient.GetLogViewerLogAsync(0, take, DirectionModel.Descending, null!, [level],
                    since, now, context.GetCancellationToken());
                total += logs.Total;
                entries.AddRange(logs.Items);
            }

            var shown = entries.OrderByDescending(x => x.Timestamp).Take(take).ToList();

            var label = lowest == LogLevelModel.Fatal ? "fatal" : $"{lowest.ToString().ToLowerInvariant()} or worse";
            if (total == 0)
            {
                await Writer.WriteLineAsync($"No {label} log entries since {since.LocalDateTime:yyyy-MM-dd HH:mm:ss}.");
                context.ExitCode = 0;
                return;
            }

            await Writer.WriteLineAsync($"{total} {label} log entries since {since.LocalDateTime:yyyy-MM-dd HH:mm:ss}:");
            foreach (var entry in shown)
            {
                await Writer.WriteLineAsync($"  {entry.Timestamp.LocalDateTime:yyyy-MM-dd HH:mm:ss} {entry.Level,-11} {OneLine(entry.RenderedMessage)}");
                if (string.IsNullOrWhiteSpace(entry.Exception) is false)
                    await Writer.WriteLineAsync($"      {OneLine(entry.Exception)}");
            }

            if (total > shown.Count)
                await Writer.WriteLineAsync($"  ... and {total - shown.Count} more (use --take to see more)");

            context.ExitCode = result.GetValueForOption(_noFail) ? 0 : 1;
        });
    }

    public static IReadOnlyList<LogLevelModel> LevelsFrom(LogLevelModel lowest)
        => Enum.GetValues<LogLevelModel>().Where(x => x >= lowest).ToList();

    private static string OneLine(string? text)
        => (text ?? string.Empty).Split('\n', 2)[0].Trim();
}
