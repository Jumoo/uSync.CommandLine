using Umbraco.Management.Api;

using uSync.Commands.Content;

namespace uSync.Files.Tests;

public class ContentUrlTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid About = Guid.NewGuid();

    private static DocumentUrlInfoModel Url(string? culture, string url) => new() { Culture = culture!, Url = url };
    private static DocumentUrlInfoModel Message(string? culture, string message) => new() { Culture = culture!, Message = message };

    private static List<ContentUrlProblem> Check(ContentUrlNode[] nodes, Dictionary<Guid, IReadOnlyCollection<DocumentUrlInfoModel>> urls)
        => ContentUrlsCommand.FindProblems(nodes, urls);

    [Test]
    public void Invariant_Content_With_A_Url_Is_Fine()
    {
        var problems = Check(
            [new(Home, "/Home", true, [""])],
            new() { [Home] = [Url("en-US", "/")] });
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Invariant_Content_With_Only_A_Message_Is_A_Problem()
    {
        var problems = Check(
            [new(Home, "/Home", true, [""])],
            new() { [Home] = [Message("en-US", "collides with /other")] });
        Assert.That(problems.Single().Message, Is.EqualTo("collides with /other"));
    }

    [Test]
    public void Content_With_No_Url_At_All_Is_A_Problem()
    {
        var problems = Check([new(Home, "/Home", true, [""])], []);
        Assert.That(problems.Single().Message, Is.EqualTo("no url"));
    }

    [Test]
    public void Variant_Content_Is_Only_Checked_In_Its_Published_Cultures()
    {
        var problems = Check(
            [new(Home, "/Home", false, ["en-US"])],
            new() { [Home] = [Url("en-US", "/"), Message("fr-FR", "not published")] });
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Variant_Content_Missing_A_Published_Culture_Url_Is_A_Problem()
    {
        var problems = Check(
            [new(Home, "/Home", false, ["en-US", "fr-FR"])],
            new() { [Home] = [Url("en-US", "/"), Message("fr-FR", "no hostname")] });
        Assert.That(problems.Single(), Is.EqualTo(new ContentUrlProblem("/Home", "fr-FR", "no hostname")));
    }

    [Test]
    public void Two_Documents_With_The_Same_Url_Clash()
    {
        var problems = Check(
            [new(Home, "/Home", true, [""]), new(About, "/About", true, [""])],
            new() { [Home] = [Url(null, "/same/")], [About] = [Url(null, "/SAME/")] });
        Assert.That(problems.Single().Path, Is.EqualTo("/About"));
        Assert.That(problems.Single().Message, Does.Contain("/Home"));
    }
}
