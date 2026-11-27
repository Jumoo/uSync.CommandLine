using Microsoft.Extensions.Configuration;

using System.CommandLine;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.Index;

public class IndexerListCommand : ConnectedCommandBase, ISyncCommand
{
    private readonly Option<bool> _failUnhealthy = new("--fail-unhealthy",
        "exit 1 if any index isn't healthy (e.g. after a rebuild)");

    private readonly Option<bool> _json = JsonOutput.CreateOption();

    public Command Command { get; }
    public IndexerListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("indexer-list", "List the indexes on the server, with their health and document counts")
        {
            _failUnhealthy
        };
        AddCoreOptions(Command);
        Command.AddOption(_json);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var indexes = await umbracoClient.GetIndexerAsync(0, 1000);
            var failUnhealthy = context.ParseResult.GetValueForOption(_failUnhealthy);

            if (context.ParseResult.GetValueForOption(_json))
            {
                await JsonOutput.WriteAsync(Writer, indexes);
                var anyUnhealthy = indexes.Items.Any(x => (x.HealthStatus?.Status ?? HealthStatusModel.Unhealthy) != HealthStatusModel.Healthy);
                context.ExitCode = anyUnhealthy && failUnhealthy ? 1 : 0;
                return;
            }

            await writer.WriteLineAsync($"Indexer Count: {indexes.Total}");

            var width = indexes.Items.Count == 0 ? 0 : indexes.Items.Max(x => x.Name?.Length ?? 0);
            var unhealthy = 0;
            foreach (var index in indexes.Items)
            {
                var status = index.HealthStatus?.Status ?? HealthStatusModel.Unhealthy;
                if (status != HealthStatusModel.Healthy) unhealthy++;

                var message = string.IsNullOrWhiteSpace(index.HealthStatus?.Message) ? "" : $" - {index.HealthStatus!.Message}";
                await writer.WriteLineAsync($"  {(index.Name ?? string.Empty).PadRight(width)}  {status,-10} {index.DocumentCount,8} docs{(index.CanRebuild ? "" : "  (can't rebuild)")}{message}");
            }

            context.ExitCode = unhealthy > 0 && failUnhealthy ? 1 : 0;
        });
    }
}
