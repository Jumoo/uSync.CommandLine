using Microsoft.Extensions.Configuration;

using System.CommandLine;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.HealthChecks;

/// <summary>
///  run every health check group, and fail on errors (or warnings).
/// </summary>
public class HealthCheckRunAllCommand : ConnectedCommandBase, ISyncCommand
{
    private readonly Option<string> _failOn = new Option<string>("--fail-on",
        () => "error", "exit 1 on results at this level or worse (none never fails)")
        .FromAmong("error", "warning", "none");

    private readonly Option<bool> _quiet = new("--quiet",
        "only list results that are warnings or errors");

    public Command Command { get; }

    public HealthCheckRunAllCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("healthcheck-run-all", "Run every health check group on the server")
        {
            _failOn, _quiet
        };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var cancel = context.GetCancellationToken();
            var quiet = context.ParseResult.GetValueForOption(_quiet);
            var failOn = context.ParseResult.GetValueForOption(_failOn) ?? "error";

            var groups = await umbracoClient.GetHealthCheckGroupAsync(0, 1000, cancel);

            int errors = 0, warnings = 0;
            foreach (var group in groups.Items)
            {
                var groupResult = await umbracoClient.PostHealthCheckGroupByNameCheckAsync(group.Name, cancel);
                await Writer.WriteLineAsync($"{group.Name}");

                foreach (var check in groupResult.Checks)
                {
                    foreach (var result in check.Results ?? [])
                    {
                        if (result.ResultType == StatusResultTypeModel.Error) errors++;
                        if (result.ResultType == StatusResultTypeModel.Warning) warnings++;

                        if (quiet && result.ResultType is not (StatusResultTypeModel.Error or StatusResultTypeModel.Warning))
                            continue;

                        await Writer.WriteLineAsync($"  {result.ResultType,-7} {OneLine(result.Message)}");
                    }
                }
            }

            await Writer.WriteLineAsync();
            await Writer.WriteLineAsync($"{groups.Items.Count} groups: {errors} error(s), {warnings} warning(s)");

            context.ExitCode = ShouldFail(failOn, errors, warnings) ? 1 : 0;
        });
    }

    public static bool ShouldFail(string failOn, int errors, int warnings) => failOn switch
    {
        "none" => false,
        "warning" => errors + warnings > 0,
        _ => errors > 0
    };

    // health check messages are html, often over several lines.
    private static string OneLine(string? text)
        => System.Text.RegularExpressions.Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
}
