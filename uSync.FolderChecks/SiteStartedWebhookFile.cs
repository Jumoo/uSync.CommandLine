using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace uSync.FolderChecks;

/// <summary>
///  builds the uSync webhook file for the "uSync Site Started" event
///  (uSync.Command.Notify), pointing at GitHub's repository_dispatch API.
/// </summary>
/// <remarks>
///  the file only names the token (uSync-Notify-Token header). The token itself
///  is set on the site, so the file is safe to commit.
/// </remarks>
public static partial class SiteStartedWebhookFile
{
    public const string EventAlias = "uSyncSiteStarted";
    public const string TokenHeader = "uSync-Notify-Token";
    public const string FolderName = "Webhooks";

    public sealed record Settings
    {
        /// <summary>owner/name of the GitHub repo the workflow is in.</summary>
        public string? Repo { get; init; }

        /// <summary>send somewhere other than GitHub (then no token header is added unless TokenName is set).</summary>
        public string? Url { get; init; }

        public string TokenName { get; init; } = "GitHubToken";

        public string? Name { get; init; }
    }

    public sealed record Result(Guid Key, string Url, XDocument Document);

    public static Result Create(Settings settings)
    {
        var url = GetUrl(settings);
        var key = KeyFor(url);
        var name = settings.Name ?? (settings.Repo is null ? "uSync site started" : $"uSync site started ({settings.Repo})");

        var headers = new XElement("Headers");
        if (settings.Repo is not null)
        {
            headers.Add(Header("Accept", "application/vnd.github+json"));
        }
        if (string.IsNullOrWhiteSpace(settings.TokenName) is false)
        {
            headers.Add(Header(TokenHeader, settings.TokenName));
        }

        var node = new XElement("Webhook",
            new XAttribute("Key", key),
            new XAttribute("Alias", url),
            new XAttribute("Level", 0),
            new XElement("Url", url),
            new XElement("Enabled", true),
            new XElement("Name", name),
            new XElement("Description", "Tells the deploy pipeline the site is running a new build. Token comes from uSync:Notify:Secrets on the site."),
            new XElement("ContentTypeKeys"),
            new XElement("Events", new XElement("Event", EventAlias)),
            headers);

        return new Result(key, url, new XDocument(new XDeclaration("1.0", "utf-8", null), node));
    }

    /// <summary>
    ///  write the file into {uSyncFolder}/Webhooks/{key}.config.
    /// </summary>
    /// <returns>the path written.</returns>
    public static string Write(string uSyncFolder, Result result)
    {
        var folder = Path.Combine(uSyncFolder, FolderName);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{result.Key}.config");
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        result.Document.Save(writer);
        return path;
    }

    private static string GetUrl(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Url) is false)
        {
            if (Uri.TryCreate(settings.Url, UriKind.Absolute, out var uri) is false || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException($"'{settings.Url}' is not an https url");
            return uri.ToString();
        }

        if (string.IsNullOrWhiteSpace(settings.Repo) || RepoPattern().IsMatch(settings.Repo) is false)
            throw new ArgumentException("--repo must be owner/name, e.g. Jumoo/my-site");

        return $"https://api.github.com/repos/{settings.Repo}/dispatches";
    }

    /// <summary>
    ///  the same url always gets the same key, so running the command again
    ///  updates the file instead of adding a second webhook.
    /// </summary>
    internal static Guid KeyFor(string url)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("usync-notify:" + url.ToLowerInvariant()));
        return new Guid(hash);
    }

    private static XElement Header(string key, string value)
        => new("Header", new XAttribute("Key", key), new XCData(value));

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex RepoPattern();
}
