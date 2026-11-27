using System.Text.Json;

using uSync.Commands.Core.Files;

namespace uSync.FolderChecks.Tests;

public class FilesToolsTests
{
    [Test]
    public void Find_By_Key_Returns_Every_File_With_That_Key()
    {
        var items = Fixtures.Verify("duplicate-key", content: true).Items;
        var found = FilesFindCommand.Find(items, "c1000000-0000-0000-0000-000000000002");
        Assert.That(found.Select(x => x.RelativePath), Is.EquivalentTo(new[] { "ContentTypes/article.config", "ContentTypes/article_copy.config" }));
    }

    [Test]
    public void Find_By_Alias_Ignores_Case_And_Matches_Every_Type()
    {
        var items = Fixtures.Verify("duplicate-key", content: true).Items;
        var found = FilesFindCommand.Find(items, "HOME");
        Assert.That(found.Select(x => x.TypeFolder), Is.EquivalentTo(new[] { "Content", "ContentTypes", "Templates" }));
    }

    [Test]
    public void Find_By_File_Name()
    {
        var items = Fixtures.Verify("duplicate-key").Items;
        Assert.That(FilesFindCommand.Find(items, "article_copy").Single().Alias, Is.EqualTo("articleCopy"));
    }

    [Test]
    public void Find_Nothing()
        => Assert.That(FilesFindCommand.Find(Fixtures.Verify("duplicate-key").Items, "nope"), Is.Empty);

    [Test]
    public void Find_Shows_Delete_Markers()
    {
        var found = FilesFindCommand.Find(Fixtures.Verify("delete-and-live").Items, "a1000000-0000-0000-0000-000000000001");
        Assert.That(found.Count(x => x.IsDelete), Is.EqualTo(1));
        Assert.That(found.Count, Is.EqualTo(2));
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static DeleteMarker Marker(string name, int? daysAgo)
        => new($"DataTypes/{name}.config", name, "DataTypes", name, daysAgo is null ? null : Now.AddDays(-daysAgo.Value));

    [Test]
    public void Clean_Selects_Only_Committed_Markers_Older_Than_The_Limit()
    {
        var markers = new[] { Marker("old", 120), Marker("new", 10), Marker("uncommitted", null) };
        Assert.That(FilesCleanCommand.Select(markers, 90, Now).Select(x => x.Alias), Is.EqualTo(new[] { "old" }));
        Assert.That(FilesCleanCommand.Select(markers, 0, Now).Select(x => x.Alias), Is.EqualTo(new[] { "old", "new" }));
        Assert.That(FilesCleanCommand.Select(markers, null, Now), Is.Empty);
    }

    private static readonly List<SyncItemChange> Changes =
    [
        new("ContentTypes", "article", "Added", "ContentTypes/article.config"),
        new("DataTypes", "Text|string", "Deleted", "DataTypes/text.config"),
    ];

    [Test]
    public void Diff_Text_Groups_By_Type()
    {
        var text = FilesDiffCommand.Format("text", "origin/main", Changes);
        Assert.That(text, Does.StartWith("2 uSync item(s) changed since origin/main:"));
        Assert.That(text, Does.Contain("  ContentTypes"));
        Assert.That(text, Does.Contain("    Added    article"));
    }

    [Test]
    public void Diff_Json_Lists_Each_Item()
    {
        var json = JsonDocument.Parse(FilesDiffCommand.Format("json", "origin/main", Changes)).RootElement;
        Assert.That(json.GetArrayLength(), Is.EqualTo(2));
        Assert.That(json[0].GetProperty("alias").GetString(), Is.EqualTo("article"));
        Assert.That(json[1].GetProperty("change").GetString(), Is.EqualTo("Deleted"));
        Assert.That(json[1].GetProperty("path").GetString(), Is.EqualTo("DataTypes/text.config"));
    }

    [Test]
    public void Diff_Markdown_Escapes_Cells()
        => Assert.That(FilesDiffCommand.Format("markdown", "origin/main", Changes), Does.Contain(@"| DataTypes | Text\|string | Deleted |"));

    [Test]
    public void Diff_With_No_Changes()
    {
        Assert.That(FilesDiffCommand.Format("text", "main", []), Does.Contain("No uSync items changed since main."));
        Assert.That(FilesDiffCommand.Format("json", "main", []).Trim(), Is.EqualTo("[]"));
    }
}
