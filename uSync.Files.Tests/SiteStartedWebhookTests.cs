using System.Net;
using System.Xml.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using uSync.Notify;
using uSync.Files;

namespace uSync.Files.Tests;

public class SiteStartedWebhookTests
{
    [Test]
    public void File_Points_At_The_Repo_Dispatch_Api()
    {
        var result = SiteStartedWebhookFile.Create(new() { Repo = "Jumoo/my-site" });
        var root = result.Document.Root!;

        Assert.That(result.Url, Is.EqualTo("https://api.github.com/repos/Jumoo/my-site/dispatches"));
        Assert.That(root.Name.LocalName, Is.EqualTo("Webhook"));
        Assert.That(root.Attribute("Key")!.Value, Is.EqualTo(result.Key.ToString()));
        Assert.That(root.Element("Url")!.Value, Is.EqualTo(result.Url));
        Assert.That(root.Element("Events")!.Elements("Event").Select(x => x.Value), Is.EqualTo(new[] { "uSyncSiteStarted" }));
    }

    [Test]
    public void File_Names_The_Token_But_Does_Not_Hold_It()
    {
        var result = SiteStartedWebhookFile.Create(new() { Repo = "Jumoo/my-site", TokenName = "GitHubToken" });
        var headers = result.Document.Root!.Element("Headers")!.Elements("Header")
            .ToDictionary(x => x.Attribute("Key")!.Value, x => x.Value);

        Assert.That(headers["uSync-Notify-Token"], Is.EqualTo("GitHubToken"));
        Assert.That(headers.ContainsKey("Authorization"), Is.False);
    }

    [Test]
    public void Same_Repo_Gets_The_Same_Key()
    {
        var a = SiteStartedWebhookFile.Create(new() { Repo = "Jumoo/my-site" });
        var b = SiteStartedWebhookFile.Create(new() { Repo = "jumoo/MY-SITE" });
        var c = SiteStartedWebhookFile.Create(new() { Repo = "Jumoo/other" });

        Assert.That(a.Key, Is.EqualTo(b.Key));
        Assert.That(a.Key, Is.Not.EqualTo(c.Key));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-repo")]
    [TestCase("a/b/c")]
    public void Bad_Repo_Is_Refused(string? repo)
        => Assert.Throws<ArgumentException>(() => SiteStartedWebhookFile.Create(new() { Repo = repo }));

    [Test]
    public void Http_Url_Is_Refused()
        => Assert.Throws<ArgumentException>(() => SiteStartedWebhookFile.Create(new() { Url = "http://example.com/hook" }));

    [Test]
    public void Write_Puts_It_In_The_Webhooks_Folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "usync-webhook-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = SiteStartedWebhookFile.Create(new() { Repo = "Jumoo/my-site" });
            var path = SiteStartedWebhookFile.Write(folder, result);

            Assert.That(path, Is.EqualTo(Path.Combine(folder, "Webhooks", $"{result.Key}.config")));
            Assert.That(XDocument.Load(path).Root!.Element("Url")!.Value, Is.EqualTo(result.Url));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [TestCase("1.0.0+4f1c2e9", "4f1c2e9")]
    [TestCase("17.0.0+1e2cad0bd4f9887c1cdd3ac4d6cc00aac011ef7d", "1e2cad0bd4f9887c1cdd3ac4d6cc00aac011ef7d")]
    [TestCase("1.2.0+build.5.1e2cad0bd4f9887c1cdd3ac4d6cc00aac011ef7d", "1e2cad0bd4f9887c1cdd3ac4d6cc00aac011ef7d")]
    [TestCase("1.2.0+build.5", "build.5")]
    [TestCase("1.0.0", null)]
    [TestCase("1.0.0+", null)]
    [TestCase(null, null)]
    public void Revision_Comes_From_The_Informational_Version(string? version, string? expected)
        => Assert.That(SiteStartedWebhookEvent.RevisionFromVersion(version), Is.EqualTo(expected));

    [TestCase("https://api.github.com/repos/a/b/dispatches", true)]
    [TestCase("https://API.GITHUB.COM/repos/a/b/dispatches", true)]
    [TestCase("http://api.github.com/repos/a/b/dispatches", false)]
    [TestCase("https://evil.example.com/hook", false)]
    [TestCase("https://api.github.com.evil.example.com/hook", false)]
    public void Token_Only_Goes_To_Allowed_Hosts(string url, bool allowed)
        => Assert.That(NotifyTokenHandler.IsAllowedHost(new Uri(url), ["api.github.com"]), Is.EqualTo(allowed));

    [Test]
    public async Task Token_Is_Added_To_The_Copy_Not_The_Original()
    {
        var (handler, inner) = CreateHandler(new NotifyOptions { Secrets = { ["GitHubToken"] = "secret-value" } });
        var request = Request("https://api.github.com/repos/a/b/dispatches");

        using var client = new HttpClient(handler);
        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(inner.Authorization, Is.EqualTo("Bearer secret-value"));
        Assert.That(inner.HadNameHeader, Is.False);
        Assert.That(inner.Body, Is.EqualTo("{\"event_type\":\"x\"}"));

        // what Umbraco logs
        Assert.That(request.Headers.Authorization, Is.Null);
        Assert.That(request.Headers.GetValues(NotifyTokenHandler.HeaderName).Single(), Is.EqualTo("GitHubToken"));
        Assert.That(await request.Content!.ReadAsStringAsync(), Is.EqualTo("{\"event_type\":\"x\"}"));
    }

    [Test]
    public async Task Token_Is_Not_Sent_To_Other_Hosts()
    {
        var (handler, inner) = CreateHandler(new NotifyOptions { Secrets = { ["GitHubToken"] = "secret-value" } });

        using var client = new HttpClient(handler);
        var response = await client.SendAsync(Request("https://evil.example.com/hook"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(inner.Called, Is.False);
    }

    [Test]
    public async Task Missing_Secret_Is_Not_Sent()
    {
        var (handler, inner) = CreateHandler(new NotifyOptions());

        using var client = new HttpClient(handler);
        var response = await client.SendAsync(Request("https://api.github.com/repos/a/b/dispatches"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(inner.Called, Is.False);
    }

    [Test]
    public async Task Requests_Without_The_Header_Pass_Through()
    {
        var (handler, inner) = CreateHandler(new NotifyOptions());
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/hook");
        request.Headers.Authorization = new("Bearer", "their-own");

        using var client = new HttpClient(handler);
        await client.SendAsync(request);

        Assert.That(inner.Authorization, Is.EqualTo("Bearer their-own"));
    }

    private static HttpRequestMessage Request(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent("{\"event_type\":\"x\"}")
        };
        request.Headers.Add(NotifyTokenHandler.HeaderName, "GitHubToken");
        return request;
    }

    private static (NotifyTokenHandler, RecordingHandler) CreateHandler(NotifyOptions options)
    {
        var inner = new RecordingHandler();
        var handler = new NotifyTokenHandler(new StaticOptions(options), NullLogger<NotifyTokenHandler>.Instance)
        {
            InnerHandler = inner
        };
        return (handler, inner);
    }

    private class RecordingHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }
        public string? Authorization { get; private set; }
        public bool HadNameHeader { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            Authorization = request.Headers.Authorization?.ToString();
            HadNameHeader = request.Headers.Contains(NotifyTokenHandler.HeaderName);
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    private class StaticOptions(NotifyOptions value) : IOptionsMonitor<NotifyOptions>
    {
        public NotifyOptions CurrentValue => value;
        public NotifyOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<NotifyOptions, string?> listener) => null;
    }
}
