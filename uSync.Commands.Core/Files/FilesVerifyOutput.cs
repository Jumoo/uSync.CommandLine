using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using uSync.FolderChecks;

namespace uSync.Commands.Core.Files;

/// <summary>
///  what a files-verify run found, ready to write out.
/// </summary>
public sealed class FilesVerifyReport
{
    public required string Folder { get; init; }

    /// <summary>
    ///  file paths in the output are relative to this (the repo root, or
    ///  GITHUB_WORKSPACE), so annotations land on the right file.
    /// </summary>
    public required string BaseFolder { get; init; }

    public required IReadOnlyList<SyncFileIssue> Issues { get; init; }

    public int FileCount { get; init; }

    public IReadOnlyList<string> SkippedFolders { get; init; } = [];

    public bool WarningsAsErrors { get; init; }

    public int ErrorCount => Issues.Count(x => x.Severity == SyncFileSeverity.Error);

    public int WarningCount => Issues.Count(x => x.Severity == SyncFileSeverity.Warning);

    public bool Failed => ErrorCount > 0 || (WarningsAsErrors && WarningCount > 0);

    public string RelativePath(string path)
        => Path.GetRelativePath(BaseFolder, path).Replace('\\', '/');

    public string SummaryLine()
    {
        var folder = RelativePath(Folder);
        var result = Issues.Count == 0
            ? "no problems found"
            : $"{Plural(ErrorCount, "error")}, {Plural(WarningCount, "warning")}";
        var skipped = SkippedFolders.Count > 0
            ? $" (skipped {string.Join(", ", SkippedFolders)} - use --content to check them)"
            : "";
        return $"uSync files-verify: {result} in {Plural(FileCount, "file")} in {folder}{skipped}";
    }

    internal static string Plural(int count, string word)
        => $"{count} {word}{(count == 1 ? "" : "s")}";
}

public static class FilesVerifyFormats
{
    public const string Text = "text";
    public const string GitHub = "github";
    public const string Sarif = "sarif";
    public const string JUnit = "junit";

    public static readonly string[] All = [Text, GitHub, Sarif, JUnit];

    public static void Write(string format, FilesVerifyReport report, TextWriter writer)
    {
        switch (format)
        {
            case GitHub: WriteGitHub(report, writer); break;
            case Sarif: WriteSarif(report, writer); break;
            case JUnit: WriteJUnit(report, writer); break;
            default: WriteText(report, writer); break;
        }
    }

    public static void WriteText(FilesVerifyReport report, TextWriter writer)
    {
        foreach (var issue in report.Issues)
        {
            var severity = issue.Severity == SyncFileSeverity.Error ? "ERROR  " : "WARNING";
            var line = issue.Line > 0 ? $":{issue.Line}" : "";
            writer.WriteLine($"{severity} {report.RelativePath(issue.FilePath)}{line} [{issue.Rule.Id} {issue.Rule.Name}]");
            writer.WriteLine($"        {issue.Message}");
        }

        if (report.Issues.Count > 0) writer.WriteLine();
        writer.WriteLine(report.SummaryLine());
    }

    /// <summary>
    ///  GitHub workflow commands, which show as annotations on the pull request.
    /// </summary>
    public static void WriteGitHub(FilesVerifyReport report, TextWriter writer)
    {
        foreach (var issue in report.Issues)
        {
            var command = issue.Severity == SyncFileSeverity.Error ? "error" : "warning";
            var properties = new List<string> { $"file={EscapeProperty(report.RelativePath(issue.FilePath))}" };
            if (issue.Line > 0) properties.Add($"line={issue.Line}");
            properties.Add($"title={EscapeProperty($"uSync {issue.Rule.Id} {issue.Rule.Name}")}");

            writer.WriteLine($"::{command} {string.Join(",", properties)}::{EscapeData(issue.Message)}");
        }

        writer.WriteLine(report.SummaryLine());
    }

    // https://github.com/actions/toolkit/blob/main/packages/core/src/command.ts
    public static string EscapeData(string value)
        => value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");

    public static string EscapeProperty(string value)
        => EscapeData(value).Replace(":", "%3A").Replace(",", "%2C");

    /// <summary>
    ///  SARIF 2.1.0, for GitHub code scanning and Azure DevOps.
    /// </summary>
    public static void WriteSarif(FilesVerifyReport report, TextWriter writer)
    {
        var rules = SyncFileRules.All;

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("$schema", "https://json.schemastore.org/sarif-2.1.0.json");
            json.WriteString("version", "2.1.0");
            json.WriteStartArray("runs");
            json.WriteStartObject();

            json.WriteStartObject("tool");
            json.WriteStartObject("driver");
            json.WriteString("name", "uSync files-verify");
            json.WriteString("informationUri", "https://github.com/Jumoo/uSync.CommandLine");
            json.WriteStartArray("rules");
            foreach (var rule in rules)
            {
                json.WriteStartObject();
                json.WriteString("id", rule.Id);
                json.WriteString("name", rule.Name);
                json.WriteStartObject("shortDescription");
                json.WriteString("text", rule.Description);
                json.WriteEndObject();
                json.WriteStartObject("defaultConfiguration");
                json.WriteString("level", SarifLevel(rule.Severity, report.WarningsAsErrors));
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject(); // driver
            json.WriteEndObject(); // tool

            json.WriteStartObject("originalUriBaseIds");
            json.WriteStartObject("SRCROOT");
            json.WriteString("uri", new Uri(Path.TrimEndingDirectorySeparator(report.BaseFolder) + Path.DirectorySeparatorChar).AbsoluteUri);
            json.WriteEndObject();
            json.WriteEndObject();

            json.WriteStartArray("results");
            foreach (var issue in report.Issues)
            {
                json.WriteStartObject();
                json.WriteString("ruleId", issue.Rule.Id);
                json.WriteNumber("ruleIndex", IndexOf(rules, issue.Rule));
                json.WriteString("level", SarifLevel(issue.Severity, report.WarningsAsErrors));
                json.WriteStartObject("message");
                json.WriteString("text", issue.Message);
                json.WriteEndObject();
                json.WriteStartArray("locations");
                json.WriteStartObject();
                json.WriteStartObject("physicalLocation");
                json.WriteStartObject("artifactLocation");
                json.WriteString("uri", EscapeUri(report.RelativePath(issue.FilePath)));
                json.WriteString("uriBaseId", "SRCROOT");
                json.WriteEndObject();
                json.WriteStartObject("region");
                json.WriteNumber("startLine", Math.Max(issue.Line, 1));
                json.WriteEndObject();
                json.WriteEndObject(); // physicalLocation
                json.WriteEndObject();
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteEndObject(); // run
            json.WriteEndArray();
            json.WriteEndObject();
        }

        writer.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static int IndexOf(IReadOnlyList<SyncFileRule> rules, SyncFileRule rule)
    {
        for (var n = 0; n < rules.Count; n++)
            if (rules[n] == rule) return n;
        return -1;
    }

    private static string SarifLevel(SyncFileSeverity severity, bool warningsAsErrors)
        => severity == SyncFileSeverity.Error || warningsAsErrors ? "error" : "warning";

    private static string EscapeUri(string relativePath)
        => string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));

    /// <summary>
    ///  JUnit XML, which Azure DevOps and GitLab show as test results.
    /// </summary>
    /// <remarks>
    ///  one test case per check, failed when the check found errors (or warnings
    ///  with --warnings-as-errors). Warnings that don't fail go in system-out.
    /// </remarks>
    public static void WriteJUnit(FilesVerifyReport report, TextWriter writer)
    {
        var cases = new List<XElement>();
        var failures = 0;

        foreach (var rule in SyncFileRules.All)
        {
            var issues = report.Issues.Where(x => x.Rule == rule).ToList();
            var testCase = new XElement("testcase",
                new XAttribute("classname", "uSync.files-verify"),
                new XAttribute("name", $"{rule.Id} {rule.Name}: {rule.Description}"));

            if (issues.Count > 0)
            {
                var details = string.Join(Environment.NewLine, issues.Select(x =>
                    $"{report.RelativePath(x.FilePath)}{(x.Line > 0 ? $":{x.Line}" : "")} {x.Message}"));

                var fails = rule.Severity == SyncFileSeverity.Error || report.WarningsAsErrors;
                if (fails)
                {
                    failures++;
                    testCase.Add(new XElement("failure",
                        new XAttribute("message", $"{issues.Count} problem(s): {issues[0].Message}"),
                        new XAttribute("type", rule.Name),
                        details));
                }
                else
                {
                    testCase.Add(new XElement("system-out", details));
                }
            }

            cases.Add(testCase);
        }

        var suite = new XElement("testsuite",
            new XAttribute("name", "uSync files-verify"),
            new XAttribute("tests", cases.Count),
            new XAttribute("failures", failures),
            new XAttribute("errors", 0),
            new XAttribute("skipped", 0),
            cases);

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("testsuites",
                new XAttribute("name", "uSync files-verify"),
                new XAttribute("tests", cases.Count),
                new XAttribute("failures", failures),
                suite));

        writer.WriteLine(doc.Declaration);
        writer.WriteLine(doc.Root);
    }
}
