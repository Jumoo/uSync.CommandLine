using uSync.Commands.Server;

namespace uSync.Files.Tests;

public class DiagnosticCommandTests
{
    private static readonly List<(string Name, string Data)> Items =
    [
        ("Umbraco Version", "17.8.1"),
        ("Server OS", "Windows | 11"),
    ];

    [Test]
    public void Troubleshooting_Text_Lines_Up_The_Values()
    {
        var lines = ServerTroubleshootCommand.ToText(Items).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines, Is.EqualTo(new[] { "Umbraco Version  17.8.1", "Server OS        Windows | 11" }));
    }

    [Test]
    public void Troubleshooting_Markdown_Escapes_Pipes()
    {
        var markdown = ServerTroubleshootCommand.ToMarkdown(Items);
        Assert.That(markdown, Does.StartWith("| | |"));
        Assert.That(markdown, Does.Contain("| Umbraco Version | 17.8.1 |"));
        Assert.That(markdown, Does.Contain(@"| Server OS | Windows \| 11 |"));
    }
}
