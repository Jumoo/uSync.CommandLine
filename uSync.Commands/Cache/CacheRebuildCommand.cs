using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Cache;

public class CacheRebuildCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public CacheRebuildCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("cache-rebuild", "Rebuild the cache on the server");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            await umbracoClient.PostPublishedCacheRebuildAsync();
            await writer.WriteLineAsync("Cache Rebuild Requested");
            context.ExitCode = 0;
        });
    }
}
