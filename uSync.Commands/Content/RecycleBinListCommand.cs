using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.Content;

/// <summary>
///  what is in the content and media recycle bins, and optionally empty them.
/// </summary>
public class RecycleBinListCommand : ConnectedCommandBase, ISyncCommand
{
    public const int DefaultTake = 50;

    private readonly Option<int> _take = new("--take",
        () => DefaultTake, "the most items to list from each bin");

    private readonly Option<bool> _empty = new("--empty",
        "empty both recycle bins (needs --yes). This permanently deletes the items.");

    private readonly Option<bool> _yes = new("--yes",
        "confirm --empty");

    private readonly Option<bool> _json = JsonOutput.CreateOption();

    public Command Command { get; }

    public RecycleBinListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("recyclebin-list", "List what is in the content and media recycle bins")
        {
            _take, _empty, _yes
        };
        AddCoreOptions(Command);
        Command.AddOption(_json);

        Command.SetHandler(async (context) =>
        {
            var empty = context.ParseResult.GetValueForOption(_empty);
            if (empty && context.ParseResult.GetValueForOption(_yes) is false)
                throw new SyncUsageException("--empty permanently deletes everything in both recycle bins; add --yes to confirm.");

            var umbracoClient = await GetUmbracoClient(context);
            var cancel = context.GetCancellationToken();
            var take = Math.Max(0, context.ParseResult.GetValueForOption(_take));

            var documents = await umbracoClient.GetRecycleBinDocumentRootAsync(0, take, cancel);
            var media = await umbracoClient.GetRecycleBinMediaRootAsync(0, take, cancel);

            if (context.ParseResult.GetValueForOption(_json))
            {
                // what was in the bins (before --empty empties them).
                await JsonOutput.WriteAsync(Writer, new { Content = documents, Media = media, Emptied = empty });
                if (empty) await EmptyBins(umbracoClient, cancel);
                context.ExitCode = 0;
                return;
            }

            await WriteBin("Content", documents.Total,
                documents.Items.Select(x => (Name(x.Variants.Select(v => v.Name)), x.HasChildren)).ToList());

            await WriteBin("Media", media.Total,
                media.Items.Select(x => (Name(x.Variants.Select(v => v.Name)), x.HasChildren)).ToList());

            if (empty)
            {
                await EmptyBins(umbracoClient, cancel);
                await Writer.WriteLineAsync();
                await Writer.WriteLineAsync("Emptied the content and media recycle bins.");
            }

            context.ExitCode = 0;
        });
    }

    private static async Task EmptyBins(Umbraco.Management.Api.UmbracoClient client, CancellationToken cancel)
    {
        await client.DeleteRecycleBinDocumentAsync(cancel);
        await client.DeleteRecycleBinMediaAsync(cancel);
    }

    private async Task WriteBin(string title, long total, IReadOnlyList<(string Name, bool HasChildren)> items)
    {
        await Writer.WriteLineAsync($"{title} recycle bin: {total} item(s) at the top level");
        foreach (var item in items)
            await Writer.WriteLineAsync($"  {item.Name}{(item.HasChildren ? " (and children)" : "")}");
        if (total > items.Count)
            await Writer.WriteLineAsync($"  ... and {total - items.Count} more (use --take)");
    }

    private static string Name(IEnumerable<string?> names)
        => names.FirstOrDefault(x => string.IsNullOrWhiteSpace(x) is false) ?? "(no name)";
}
