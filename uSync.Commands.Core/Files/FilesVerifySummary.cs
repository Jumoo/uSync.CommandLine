using System.Text;
using System.Xml.Linq;

using uSync.Commands.Core.Output;
using uSync.Files;

namespace uSync.Commands.Core.Files;

/// <summary>
///  a uSync item changed on this branch.
/// </summary>
public sealed record SyncItemChange(string Type, string Alias, string Change, string RelativePath);

/// <summary>
///  markdown summary of a files-verify run, for $GITHUB_STEP_SUMMARY.
/// </summary>
public static class FilesVerifySummary
{
    private const int MaxRows = 200;

    /// <summary>
    ///  work out what changed for each changed file in the uSync folder.
    /// </summary>
    public static IReadOnlyList<SyncItemChange> GetItemChanges(string folder, IEnumerable<GitChange> changes, string gitRef)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        var result = new List<SyncItemChange>();

        foreach (var change in changes.Where(x => IsUnder(root, x.FilePath)))
        {
            if (Path.GetExtension(change.FilePath).Equals(".config", StringComparison.OrdinalIgnoreCase) is false) continue;

            var relative = SyncFolderVerifier.GetRelativePath(root, change.FilePath);
            var type = relative.Contains('/') ? relative[..relative.IndexOf('/')] : "uSync";

            var text = change.Kind == GitChangeKind.Deleted
                ? GitChanges.GetFileAtRef(change.FilePath, gitRef)
                : File.Exists(change.FilePath) ? File.ReadAllText(change.FilePath) : null;

            var node = TryParse(text);
            var alias = node?.Attribute("Alias")?.Value;
            if (string.IsNullOrWhiteSpace(alias)) alias = Path.GetFileNameWithoutExtension(change.FilePath);

            var kind = change.Kind switch
            {
                GitChangeKind.Added => "Added",
                GitChangeKind.Deleted => "Deleted",
                _ => "Changed"
            };

            // uSync records a delete as an <Empty Change="Delete"> file.
            if (change.Kind != GitChangeKind.Deleted
                && node?.Name.LocalName == "Empty"
                && string.Equals(node.Attribute("Change")?.Value, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                kind = "Deleted";
            }

            result.Add(new SyncItemChange(type, alias, kind, relative));
        }

        return result
            .OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsUnder(string root, string path)
        => path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static XElement? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return XElement.Parse(text.TrimStart('﻿')); }
        catch { return null; }
    }

    public static string Build(FilesVerifyReport report, string? gitRef, IReadOnlyList<SyncItemChange>? changes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### uSync files-verify");
        sb.AppendLine();

        var status = report.Failed ? ":x:" : report.Issues.Count > 0 ? ":warning:" : ":white_check_mark:";
        var result = report.Issues.Count == 0
            ? "No problems found"
            : $"{FilesVerifyReport.Plural(report.ErrorCount, "error")}, {FilesVerifyReport.Plural(report.WarningCount, "warning")}";
        sb.AppendLine($"{status} {result} in `{report.RelativePath(report.Folder)}`.");
        if (report.SkippedFolders.Count > 0)
            sb.AppendLine($"Not checked: {string.Join(", ", report.SkippedFolders)} (use `--content` to check them).");
        sb.AppendLine();

        if (report.Issues.Count > 0)
        {
            sb.AppendLine("| | File | Problem |");
            sb.AppendLine("|---|---|---|");
            foreach (var issue in report.Issues.Take(MaxRows))
            {
                var icon = issue.Severity == SyncFileSeverity.Error ? ":x:" : ":warning:";
                var line = issue.Line > 0 ? $":{issue.Line}" : "";
                sb.AppendLine($"| {icon} | `{StepSummary.Cell(report.RelativePath(issue.FilePath))}{line}` | {StepSummary.Cell(issue.Message)} |");
            }
            if (report.Issues.Count > MaxRows)
                sb.AppendLine($"| | | ... and {report.Issues.Count - MaxRows} more |");
            sb.AppendLine();
        }

        if (gitRef is null || changes is null)
        {
            sb.AppendLine("_Use `--changed-since <ref>` to list the items this branch changes._");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine($"#### Items changed since `{StepSummary.Cell(gitRef)}`");
        sb.AppendLine();

        if (changes.Count == 0)
        {
            sb.AppendLine("No uSync items changed.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine($"{string.Join(", ", changes.GroupBy(x => x.Change).OrderBy(x => x.Key).Select(x => $"{x.Count()} {x.Key.ToLowerInvariant()}"))}");
        sb.AppendLine();
        sb.AppendLine("| Type | Alias | Change |");
        sb.AppendLine("|---|---|---|");
        foreach (var change in changes.Take(MaxRows))
            sb.AppendLine($"| {StepSummary.Cell(change.Type)} | {StepSummary.Cell(change.Alias)} | {change.Change} |");
        if (changes.Count > MaxRows)
            sb.AppendLine($"| | ... and {changes.Count - MaxRows} more | |");
        sb.AppendLine();

        return sb.ToString();
    }
}
