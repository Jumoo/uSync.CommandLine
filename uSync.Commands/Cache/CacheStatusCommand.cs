using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Cache;

public class CacheStatusCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public CacheStatusCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("cache-status", "Show whether the cache is being rebuilt on the server");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var status = await umbracoClient.GetPublishedCacheRebuildStatusAsync(context.GetCancellationToken());
            await Writer.WriteLineAsync(status.IsRebuilding ? "Cache: rebuilding" : "Cache: idle");
            context.ExitCode = 0;
        });
    }
}
