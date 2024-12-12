using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Cache;

public class CacheReloadCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }
    public CacheReloadCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("cache-reload", "Reload the cache on the server");
        AddCoreOptions(Command);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            await umbracoClient.PostPublishedCacheReloadAsync();
            await writer.WriteLineAsync("Cache Reload Requested");
            context.ExitCode = 0;
        });
    }
}