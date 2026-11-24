using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Content;

/// <summary>
///  a published document, and the cultures it is published in ("" for invariant content).
/// </summary>
public sealed record ContentUrlNode(Guid Id, string Path, bool Invariant, IReadOnlyCollection<string> PublishedCultures);

public sealed record ContentUrlProblem(string Path, string Culture, string Message);

/// <summary>
///  walk the content tree and report published documents without a working url
///  (collisions, unpublished parents, no domain for a culture).
/// </summary>
/// <remarks>
///  Umbraco works out the problems: a url it can't route comes back as a message
///  instead of a url. This only asks for the published cultures, as an unpublished
///  culture always has a "not published" message.
/// </remarks>
public class ContentUrlsCommand : ConnectedCommandBase, ISyncCommand
{
    private const int PageSize = 100;
    private const int UrlBatchSize = 25;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Command Command { get; }

    public ContentUrlsCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("content-urls", "Check every published document has a working url (no collisions, no unpublished parents). Exits 1 on problems.");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var cancel = context.GetCancellationToken();

            var nodes = new List<ContentUrlNode>();
            await Walk(umbracoClient, null, string.Empty, nodes, cancel);

            var urls = new Dictionary<Guid, IReadOnlyCollection<DocumentUrlInfoModel>>();
            foreach (var batch in nodes.Chunk(UrlBatchSize))
            {
                foreach (var info in await GetUrls(context, batch.Select(x => x.Id), cancel))
                    urls[info.Id] = info.UrlInfos.ToList();
            }

            var problems = FindProblems(nodes, urls);

            await Writer.WriteLineAsync($"Checked {nodes.Count} published documents.");
            if (problems.Count == 0)
            {
                await Writer.WriteLineAsync("No url problems found.");
                context.ExitCode = 0;
                return;
            }

            await Writer.WriteLineAsync($"{problems.Count} url problem(s):");
            foreach (var problem in problems)
            {
                var culture = string.IsNullOrEmpty(problem.Culture) ? "" : $" [{problem.Culture}]";
                await Writer.WriteLineAsync($"  {problem.Path}{culture}: {problem.Message}");
            }

            context.ExitCode = 1;
        });
    }

    private static async Task Walk(UmbracoClient client, Guid? parent, string parentPath, List<ContentUrlNode> nodes, CancellationToken cancel)
    {
        var skip = 0;
        while (true)
        {
            var page = parent is null
                ? await client.GetTreeDocumentRootAsync(skip, PageSize, null, cancel)
                : await client.GetTreeDocumentChildrenAsync(parent, skip, PageSize, null, cancel);

            foreach (var item in page.Items)
            {
                var name = item.Variants.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Name) is false)?.Name ?? item.Id.ToString();
                var path = $"{parentPath}/{name}";

                var published = item.Variants
                    .Where(x => x.State is DocumentVariantStateModel.Published or DocumentVariantStateModel.PublishedPendingChanges)
                    .Select(x => x.Culture ?? string.Empty)
                    .ToList();

                if (published.Count > 0)
                    nodes.Add(new ContentUrlNode(item.Id, path, item.Variants.All(x => x.Culture is null), published));

                if (item.HasChildren)
                    await Walk(client, item.Id, path, nodes, cancel);
            }

            skip += page.Items.Count;
            if (page.Items.Count == 0 || skip >= page.Total) return;
        }
    }

    /// <remarks>
    ///  the generated client sends the ids as one comma separated value (id=a,b),
    ///  which Umbraco doesn't read, so this builds the query (id=a&amp;id=b) itself.
    /// </remarks>
    private async Task<ICollection<DocumentUrlInfoResponseModel>> GetUrls(InvocationContext context, IEnumerable<Guid> ids, CancellationToken cancel)
    {
        var auth = GetConnectionPartameters(context);
        var query = string.Join("&", ids.Select(x => $"id={x}"));
        var result = await Client.GetFromJsonAsync<ICollection<DocumentUrlInfoResponseModel>>(
            new Uri(auth.Url, $"umbraco/management/api/v1/document/urls?{query}"), JsonOptions, cancel);
        return result ?? [];
    }

    public static List<ContentUrlProblem> FindProblems(
        IEnumerable<ContentUrlNode> nodes,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<DocumentUrlInfoModel>> urls)
    {
        var problems = new List<ContentUrlProblem>();
        var owners = new Dictionary<string, ContentUrlNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            var infos = urls.TryGetValue(node.Id, out var found) ? found : [];

            if (node.Invariant)
            {
                if (infos.Any(x => string.IsNullOrWhiteSpace(x.Url) is false) is false)
                {
                    var messages = infos.Where(x => string.IsNullOrWhiteSpace(x.Message) is false).ToList();
                    if (messages.Count == 0)
                        problems.Add(new ContentUrlProblem(node.Path, string.Empty, "no url"));
                    foreach (var message in messages)
                        problems.Add(new ContentUrlProblem(node.Path, message.Culture ?? string.Empty, message.Message));
                }
            }
            else
            {
                foreach (var culture in node.PublishedCultures)
                {
                    var forCulture = infos.Where(x => string.Equals(x.Culture, culture, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (forCulture.Any(x => string.IsNullOrWhiteSpace(x.Url) is false)) continue;

                    var messages = forCulture.Where(x => string.IsNullOrWhiteSpace(x.Message) is false).ToList();
                    if (messages.Count == 0)
                        problems.Add(new ContentUrlProblem(node.Path, culture, "no url"));
                    foreach (var message in messages)
                        problems.Add(new ContentUrlProblem(node.Path, culture, message.Message));
                }
            }

            // Umbraco reports most collisions itself; this catches the rest.
            foreach (var url in infos.Where(x => string.IsNullOrWhiteSpace(x.Url) is false).Select(x => x.Url).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (owners.TryGetValue(url, out var owner) && owner.Id != node.Id)
                    problems.Add(new ContentUrlProblem(node.Path, string.Empty, $"same url as {owner.Path} ({url})"));
                else
                    owners[url] = node;
            }
        }

        return problems;
    }
}
