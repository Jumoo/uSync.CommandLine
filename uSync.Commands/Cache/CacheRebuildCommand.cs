using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.Diagnostics;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Cache;

public class CacheRebuildCommand : ConnectedCommandBase, ISyncCommand
{
    public const int DefaultTimeoutSeconds = 600;
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(3);

    private readonly Option<bool> _wait = new("--wait",
        "wait for the rebuild to finish");

    private readonly Option<int> _timeout = new("--timeout",
        () => DefaultTimeoutSeconds, "with --wait, seconds to wait before giving up");

    public Command Command { get; }

    public CacheRebuildCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("cache-rebuild", "Rebuild the cache on the server")
        {
            _wait, _timeout
        };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var cancel = context.GetCancellationToken();
            await umbracoClient.PostPublishedCacheRebuildAsync(cancel);
            await Writer.WriteLineAsync("Cache Rebuild Requested");

            if (context.ParseResult.GetValueForOption(_wait) is false)
            {
                context.ExitCode = 0;
                return;
            }

            var timeout = TimeSpan.FromSeconds(Math.Max(1, context.ParseResult.GetValueForOption(_timeout)));
            var elapsed = Stopwatch.StartNew();

            while (elapsed.Elapsed < timeout)
            {
                await Task.Delay(PollDelay, cancel);
                var status = await umbracoClient.GetPublishedCacheRebuildStatusAsync(cancel);
                if (status.IsRebuilding is false)
                {
                    await Writer.WriteLineAsync();
                    await Writer.WriteLineAsync($"Cache rebuilt ({elapsed.Elapsed.TotalSeconds:0}s)");
                    context.ExitCode = 0;
                    return;
                }
                await Writer.WriteAsync(".");
            }

            await Writer.WriteLineAsync();
            await Writer.WriteLineAsync($"ERROR: the cache was still rebuilding after {timeout.TotalSeconds:0}s.");
            context.ExitCode = 1;
        });
    }
}
