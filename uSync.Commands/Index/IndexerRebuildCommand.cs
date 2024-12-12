using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Index;

public class IndexerRebuildCommand : ConnectedCommandBase, ISyncCommand
{
    protected Option<string> indexName = new Option<string>(new string[] { "--index", "-n" }, "the name of the index to rebuild");

    public Command Command { get; }
    public IndexerRebuildCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("indexer-rebuild", "Rebuild the index on the server");
        AddCoreOptions(Command);
        Command.AddOption(indexName);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);

            var indexName = context.ParseResult.GetValueForOption(this.indexName) ??
                throw new Exception("No index name given");

            await umbracoClient.PostIndexerByIndexNameRebuildAsync(indexName);
            await writer.WriteLineAsync($"Indexer [{indexName}] Rebuild Requested");
            context.ExitCode = 0;
        });
    }
}
