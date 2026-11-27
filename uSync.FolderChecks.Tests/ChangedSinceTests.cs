using System.Diagnostics;

using uSync.Commands.Core.Files;
using uSync.FolderChecks;

namespace uSync.FolderChecks.Tests;

/// <summary>
///  --changed-since against a real (temporary) git repository.
/// </summary>
public class ChangedSinceTests
{
    private string _repo = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _repo = Fixtures.CopyToTemp("clean");
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "test");
        Git("config", "core.autocrlf", "false");
        Git("add", "-A");
        Git("commit", "-q", "-m", "base");
        Git("checkout", "-q", "-b", "feature");
    }

    [TearDown]
    public void TearDown() => Fixtures.DeleteFolder(_repo);

    private string Folder => Path.Combine(_repo, "uSync", "v17");

    private void Git(params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repo, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) Assert.Fail($"git {string.Join(' ', args)}: {error}");
    }

    private void CopyFixtureFile(string fixture, string relative)
        => File.Copy(Path.Combine(Fixtures.Folder(fixture), relative), Path.Combine(Folder, relative), true);

    [Test]
    public void Lists_Committed_Uncommitted_And_Untracked_Changes()
    {
        CopyFixtureFile("alias-clash-two-branches", "ContentTypes/article_1.config");
        Git("add", "-A");
        Git("commit", "-q", "-m", "add article");

        File.AppendAllText(Path.Combine(Folder, "Templates", "home.config"), "\n");
        File.Delete(Path.Combine(Folder, "MediaTypes", "image.config"));
        File.WriteAllText(Path.Combine(Folder, "DataTypes", "New.config"), "<DataType Key=\"a1000000-0000-0000-0000-0000000000c1\" Alias=\"New\" />");

        var changes = GitChanges.GetChanges(Folder, "main").ToDictionary(x => Path.GetFileName(x.FilePath), x => x.Kind);

        Assert.That(changes["article_1.config"], Is.EqualTo(GitChangeKind.Added));
        Assert.That(changes["home.config"], Is.EqualTo(GitChangeKind.Changed));
        Assert.That(changes["image.config"], Is.EqualTo(GitChangeKind.Deleted));
        Assert.That(changes["New.config"], Is.EqualTo(GitChangeKind.Added));
        Assert.That(changes, Has.Count.EqualTo(4));
    }

    [Test]
    public void Only_Reports_Problems_In_Changed_Files_But_Finds_Clashes_With_Unchanged_Ones()
    {
        // this branch adds a second 'article', main already has one.
        CopyFixtureFile("alias-clash-two-branches", "ContentTypes/article_1.config");
        Git("add", "-A");
        Git("commit", "-q", "-m", "add article");

        var output = new StringWriter();
        var exit = new FilesVerifyRunner(output, new StringWriter(), _ => null).Run(new FilesVerifySettings
        {
            Folder = Folder,
            Format = "text",
            ChangedSince = "main"
        });

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("article_1.config"));
        Assert.That(output.ToString(), Does.Not.Contain("ContentTypes/article.config:"));
        Assert.That(output.ToString(), Does.Contain("1 error"));
    }

    [Test]
    public void Problems_In_Unchanged_Files_Are_Ignored()
    {
        // broken on main already - not this branch's problem.
        Fixtures.CopyDirectory(Fixtures.Folder("invalid-xml"), Folder);
        Git("add", "-A");
        Git("commit", "-q", "-m", "broken on base");
        Git("checkout", "-q", "-b", "feature2");

        var exit = new FilesVerifyRunner(new StringWriter(), new StringWriter(), _ => null).Run(new FilesVerifySettings
        {
            Folder = Folder,
            ChangedSince = "feature"
        });

        Assert.That(exit, Is.EqualTo(0));
    }

    [Test]
    public void Summary_Lists_Changed_Items()
    {
        CopyFixtureFile("alias-clash-two-branches", "ContentTypes/article_1.config");
        File.WriteAllText(Path.Combine(Folder, "DataTypes", "Textstring_deleted.config"),
            "<Empty Key=\"a1000000-0000-0000-0000-000000000009\" Alias=\"Old Type\" Change=\"Delete\" />");
        File.Delete(Path.Combine(Folder, "Templates", "home.config"));
        Git("add", "-A");
        Git("commit", "-q", "-m", "changes");

        var summaryFile = Path.Combine(_repo, "summary.md");
        new FilesVerifyRunner(new StringWriter(), new StringWriter(), name => name == "GITHUB_STEP_SUMMARY" ? summaryFile : null).Run(new FilesVerifySettings
        {
            Folder = Folder,
            Format = "text",
            ChangedSince = "main",
            Summary = true
        });

        var markdown = File.ReadAllText(summaryFile);
        Assert.That(markdown, Does.Contain("#### Items changed since `main`"));
        Assert.That(markdown, Does.Contain("| ContentTypes | article | Added |"));
        Assert.That(markdown, Does.Contain("| DataTypes | Old Type | Deleted |"));
        // deleted file: the alias comes from the file in git.
        Assert.That(markdown, Does.Contain("| Templates | home | Deleted |"));
    }

    [Test]
    public void Unknown_Ref_Exits_2()
    {
        var error = new StringWriter();
        var exit = new FilesVerifyRunner(new StringWriter(), error, _ => null).Run(new FilesVerifySettings
        {
            Folder = Folder,
            ChangedSince = "origin/does-not-exist"
        });

        Assert.That(exit, Is.EqualTo(2));
        Assert.That(error.ToString(), Does.Contain("git"));
    }

    [Test]
    public void Option_Like_Ref_Is_Rejected()
    {
        Assert.Throws<GitException>(() => GitChanges.GetChanges(Folder, "--output=/tmp/x"));
    }
}
