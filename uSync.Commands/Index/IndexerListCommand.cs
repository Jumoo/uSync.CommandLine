using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Index;

public class IndexerListCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }
    public IndexerListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("indexer-list", "List the indexes on the server");
        AddCoreOptions(Command);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var indexes = await umbracoClient.GetIndexerAsync(0, 1000);
            await writer.WriteLineAsync($"Indexer Count: {indexes.Total}");
            foreach (var index in indexes.Items)
            {
                await writer.WriteLineAsync($"Index: {index.Name} {index.CanRebuild} {index.DocumentCount}");
            }
            context.ExitCode = 0;
        });
    }
}
