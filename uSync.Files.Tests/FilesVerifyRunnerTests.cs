using System.Text.Json;
using System.Xml.Linq;

using uSync.Commands.Core.Files;
using uSync.Files;

namespace uSync.Files.Tests;

public class FilesVerifyRunnerTests
{
    private readonly List<string> _temp = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var folder in _temp) Fixtures.DeleteFolder(folder);
        _temp.Clear();
    }

    private string Temp(string? fixture = null)
    {
        var folder = fixture is null ? Fixtures.TempFolder() : Fixtures.CopyToTemp(fixture);
        _temp.Add(folder);
        return folder;
    }

    private static (int Exit, string Output, string Error) Run(FilesVerifySettings settings, Dictionary<string, string>? environment = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var env = environment ?? [];
        var exit = new FilesVerifyRunner(output, error, name => env.TryGetValue(name, out var value) ? value : null).Run(settings);
        return (exit, output.ToString(), error.ToString());
    }

    [Test]
    public void Clean_Folder_Exits_0()
    {
        var (exit, output, _) = Run(new FilesVerifySettings { Folder = Fixtures.Folder("clean"), Format = "text" });
        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output, Does.Contain("no problems found"));
    }

    [Test]
    public void Errors_Exit_1()
    {
        var (exit, output, _) = Run(new FilesVerifySettings { Folder = Fixtures.Folder("alias-clash-two-branches"), Format = "text" });
        Assert.That(exit, Is.EqualTo(1));
        Assert.That(output, Does.Contain("ERROR").And.Contain("USYNC004"));
    }

    [Test]
    public void Warnings_Exit_0_Unless_Warnings_As_Errors()
    {
        var folder = Fixtures.Folder("broken-references");
        Assert.That(Run(new FilesVerifySettings { Folder = folder, Format = "text" }).Exit, Is.EqualTo(0));
        Assert.That(Run(new FilesVerifySettings { Folder = folder, Format = "text", WarningsAsErrors = true }).Exit, Is.EqualTo(1));
    }

    [Test]
    public void Missing_Folder_Exits_2()
    {
        var (exit, _, error) = Run(new FilesVerifySettings { Folder = Path.Combine(Temp(), "nope") });
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(error, Does.Contain("not found"));
    }

    [Test]
    public void No_Folder_To_Discover_Exits_2()
    {
        var (exit, _, error) = Run(new FilesVerifySettings { WorkingFolder = Temp() });
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(error, Does.Contain("No uSync folder found"));
    }

    [Test]
    public void Bad_Format_Exits_2()
    {
        var (exit, _, _) = Run(new FilesVerifySettings { Folder = Fixtures.Folder("clean"), Format = "xml" });
        Assert.That(exit, Is.EqualTo(2));
    }

    [Test]
    public void Format_Is_GitHub_On_Actions()
    {
        var site = Fixtures.Site("alias-clash-two-branches");
        var (_, output, _) = Run(
            new FilesVerifySettings { Folder = Fixtures.Folder("alias-clash-two-branches") },
            new() { ["GITHUB_ACTIONS"] = "true", ["GITHUB_WORKSPACE"] = site });

        Assert.That(output, Does.Contain("::error file=uSync/v17/ContentTypes/article_1.config,line=2,title=uSync USYNC004 alias-clash::"));
    }

    [Test]
    public void GitHub_Annotations_Escape_Values()
    {
        Assert.That(FilesVerifyFormats.EscapeData("50%\nnext"), Is.EqualTo("50%25%0Anext"));
        Assert.That(FilesVerifyFormats.EscapeProperty("a:b,c"), Is.EqualTo("a%3Ab%2Cc"));
    }

    [Test]
    public void Sarif_Output_Is_Valid_2_1_0()
    {
        var site = Fixtures.Site("alias-clash-two-branches");
        var (exit, output, error) = Run(
            new FilesVerifySettings { Folder = Fixtures.Folder("alias-clash-two-branches"), Format = "sarif" },
            new() { ["GITHUB_WORKSPACE"] = site });

        Assert.That(exit, Is.EqualTo(1));
        using var doc = JsonDocument.Parse(output);
        Assert.That(doc.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));

        var run = doc.RootElement.GetProperty("runs")[0];
        Assert.That(run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength(), Is.EqualTo(SyncFileRules.All.Count));

        var results = run.GetProperty("results");
        Assert.That(results.GetArrayLength(), Is.EqualTo(2));
        var location = results[0].GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.That(location.GetProperty("artifactLocation").GetProperty("uri").GetString(), Does.StartWith("uSync/v17/ContentTypes/article"));
        Assert.That(location.GetProperty("region").GetProperty("startLine").GetInt32(), Is.EqualTo(2));

        // the human summary goes to stderr, keeping stdout valid json.
        Assert.That(error, Does.Contain("2 errors"));
    }

    [Test]
    public void JUnit_Output_Has_A_Failure_Per_Failing_Check()
    {
        var file = Path.Combine(Temp(), "out", "results.xml");
        var (exit, output, _) = Run(new FilesVerifySettings
        {
            Folder = Fixtures.Folder("broken-references"),
            Format = "junit",
            Output = file,
            WarningsAsErrors = true
        });

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(output, Does.Contain("junit written to"));

        var doc = XDocument.Load(file);
        var suite = doc.Root!.Element("testsuite")!;
        Assert.That((int)suite.Attribute("tests")!, Is.EqualTo(SyncFileRules.All.Count));
        Assert.That((int)suite.Attribute("failures")!, Is.EqualTo(1));
        Assert.That(suite.Elements("testcase").Single(x => x.Element("failure") is not null).Attribute("name")!.Value, Does.StartWith("USYNC101"));
    }

    [Test]
    public void JUnit_Warnings_Do_Not_Fail_By_Default()
    {
        var (_, output, _) = Run(new FilesVerifySettings { Folder = Fixtures.Folder("broken-references"), Format = "junit" });
        var doc = XDocument.Parse(output);
        Assert.That((int)doc.Root!.Attribute("failures")!, Is.EqualTo(0));
        Assert.That(doc.Descendants("system-out").Count(), Is.EqualTo(1));
    }

    [Test]
    public void Discovers_Folder_From_AppSettings()
    {
        var site = Temp("clean");
        Directory.Move(Path.Combine(site, "uSync", "v17"), Path.Combine(site, "uSync", "custom"));
        File.WriteAllText(Path.Combine(site, "appsettings.json"),
            """
            {
              // comments are allowed
              "uSync": { "Settings": { "RootFolder": "~/uSync/custom/" } }
            }
            """);

        var location = SyncFolderLocator.Discover(site);
        Assert.That(location.Folder, Is.EqualTo(Path.Combine(site, "uSync", "custom")));
        Assert.That(location.SiteRoot, Is.EqualTo(site));
    }

    [Test]
    public void Discovers_Last_Of_Folders_From_AppSettings()
    {
        var site = Temp("clean");
        Directory.CreateDirectory(Path.Combine(site, "uSync", "Root"));
        File.WriteAllText(Path.Combine(site, "appsettings.json"),
            """{ "uSync": { "Settings": { "Folders": [ "uSync/Root/", "uSync/v17/" ] } } }""");

        Assert.That(SyncFolderLocator.Discover(site).Folder, Is.EqualTo(Path.Combine(site, "uSync", "v17")));
    }

    [Test]
    public void Discovers_Highest_Version_Folder_In_A_Sub_Folder()
    {
        var repo = Temp();
        var site = Path.Combine(repo, "src", "MySite");
        Fixtures.CopyDirectory(Fixtures.Site("clean"), site);
        Directory.CreateDirectory(Path.Combine(site, "uSync", "v9"));
        Directory.CreateDirectory(Path.Combine(site, "uSync", "v16"));

        var location = SyncFolderLocator.Discover(repo);
        Assert.That(location.Folder, Is.EqualTo(Path.Combine(site, "uSync", "v17")));
        Assert.That(location.SiteRoot, Is.EqualTo(site));
    }

    [Test]
    public void Two_Sites_Need_A_Folder()
    {
        var repo = Temp();
        Fixtures.CopyDirectory(Fixtures.Site("clean"), Path.Combine(repo, "SiteA"));
        Fixtures.CopyDirectory(Fixtures.Site("clean"), Path.Combine(repo, "SiteB"));

        var ex = Assert.Throws<SyncFolderNotFoundException>(() => SyncFolderLocator.Discover(repo));
        Assert.That(ex!.Message, Does.Contain("--folder"));
    }

    [Test]
    public void Publish_Folder_Missing_uSync_Is_An_Error()
    {
        var site = Temp("clean");
        var publish = Path.Combine(site, "publish");
        Directory.CreateDirectory(publish);

        var (exit, output, _) = Run(new FilesVerifySettings { WorkingFolder = site, PublishFolder = "publish", Format = "text" });
        Assert.That(exit, Is.EqualTo(1));
        Assert.That(output, Does.Contain("USYNC201").And.Contain("no 'uSync/v17' folder"));
    }

    [Test]
    public void Publish_Folder_With_Missing_And_Changed_Files_Is_An_Error()
    {
        var site = Temp("clean");
        var publish = Path.Combine(site, "publish");
        Fixtures.CopyDirectory(Path.Combine(site, "uSync"), Path.Combine(publish, "uSync"));
        File.Delete(Path.Combine(publish, "uSync", "v17", "Templates", "home.config"));
        File.AppendAllText(Path.Combine(publish, "uSync", "v17", "DataTypes", "Textstring.config"), " ");

        var issues = SyncPublishFolderCheck.Check(Path.Combine(site, "uSync", "v17"), site, publish);
        Assert.That(issues, Has.Count.EqualTo(2));
        Assert.That(issues, Has.One.Property(nameof(SyncFileIssue.Message)).Contains("Templates/home.config' is missing"));
        Assert.That(issues, Has.One.Property(nameof(SyncFileIssue.Message)).Contains("Textstring.config' in the publish output is different"));
    }

    [Test]
    public void Publish_Folder_With_Everything_Is_Fine()
    {
        var site = Temp("clean");
        Fixtures.CopyDirectory(Path.Combine(site, "uSync"), Path.Combine(site, "out", "uSync"));

        var (exit, _, _) = Run(new FilesVerifySettings { WorkingFolder = site, PublishFolder = "out", Format = "text" });
        Assert.That(exit, Is.EqualTo(0));
    }

    [Test]
    public void Summary_Goes_To_Step_Summary_File()
    {
        var summary = Path.Combine(Temp(), "summary.md");
        var (_, output, _) = Run(
            new FilesVerifySettings { Folder = Fixtures.Folder("alias-clash-two-branches"), Format = "text", Summary = true },
            new() { ["GITHUB_STEP_SUMMARY"] = summary });

        var markdown = File.ReadAllText(summary);
        Assert.That(markdown, Does.Contain("### uSync files-verify").And.Contain(":x: 2 errors"));
        Assert.That(markdown, Does.Contain("| :x: | `"));
        Assert.That(output, Does.Not.Contain("### uSync files-verify"));
    }

    [Test]
    public void Summary_Goes_To_Console_Without_Step_Summary()
    {
        var (_, output, _) = Run(new FilesVerifySettings { Folder = Fixtures.Folder("clean"), Format = "text", Summary = true });
        Assert.That(output, Does.Contain("### uSync files-verify").And.Contain(":white_check_mark: No problems found"));
    }
}
