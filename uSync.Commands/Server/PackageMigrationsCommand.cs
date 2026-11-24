using Microsoft.Extensions.Configuration;

using System.CommandLine;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.Server;

/// <summary>
///  list packages with pending migrations, and optionally run them.
/// </summary>
public class PackageMigrationsCommand : ConnectedCommandBase, ISyncCommand
{
    private readonly Option<bool> _run = new("--run",
        "run the pending migrations");

    private readonly Option<bool> _failOnPending = new("--fail-on-pending",
        "exit 1 if any package has pending migrations (without --run)");

    private readonly Option<bool> _json = JsonOutput.CreateOption();

    public Command Command { get; }

    public PackageMigrationsCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("package-migrations", "List the packages with pending migrations, and run them with --run")
        {
            _run, _failOnPending
        };
        AddCoreOptions(Command);
        Command.AddOption(_json);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var cancel = context.GetCancellationToken();

            var status = await umbracoClient.GetPackageMigrationStatusAsync(0, 1000, cancel);
            var pending = status.Items
                .Where(x => x.HasPendingMigrations)
                .Select(x => x.PackageName)
                .ToList();

            var json = context.ParseResult.GetValueForOption(_json);
            var run = context.ParseResult.GetValueForOption(_run);

            if (json && (run is false || pending.Count == 0))
            {
                await JsonOutput.WriteAsync(Writer, status.Items);
                context.ExitCode = pending.Count > 0 && run is false && context.ParseResult.GetValueForOption(_failOnPending) ? 1 : 0;
                return;
            }

            if (pending.Count == 0)
            {
                await Writer.WriteLineAsync("No pending package migrations.");
                context.ExitCode = 0;
                return;
            }

            if (run is false)
            {
                await Writer.WriteLineAsync($"{pending.Count} package(s) with pending migrations:");
                foreach (var name in pending)
                    await Writer.WriteLineAsync($"  {name}");

                context.ExitCode = context.ParseResult.GetValueForOption(_failOnPending) ? 1 : 0;
                return;
            }

            var results = new List<MigrationResult>();
            foreach (var name in pending)
            {
                try
                {
                    await umbracoClient.PostPackageByNameRunMigrationAsync(name, cancel);
                    results.Add(new MigrationResult(name, true, null));
                    if (json is false) await Writer.WriteLineAsync($"  {name}: migrated");
                }
                catch (ApiException ex)
                {
                    var error = $"({ex.StatusCode}) {ex.Message.Split('\n')[0]}";
                    results.Add(new MigrationResult(name, false, error));
                    if (json is false) await Writer.WriteLineAsync($"  {name}: FAILED {error}");
                }
            }

            if (json) await JsonOutput.WriteAsync(Writer, results);

            var failed = results.Count(x => x.Migrated is false);

            context.ExitCode = failed > 0 ? 1 : 0;
        });
    }

    private sealed record MigrationResult(string PackageName, bool Migrated, string? Error);
}
