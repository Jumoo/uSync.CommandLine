using uSync.Commands.Core.Output;
using uSync.Files;

namespace uSync.Commands.Core.Files;

public sealed class FilesVerifySettings
{
    public string? Folder { get; init; }
    public bool Content { get; init; }
    public string? Format { get; init; }
    public string? Output { get; init; }
    public bool WarningsAsErrors { get; init; }
    public string? ChangedSince { get; init; }
    public bool Summary { get; init; }
    public string? PublishFolder { get; init; }

    /// <summary>
    ///  where to start looking for the uSync folder (the current directory).
    /// </summary>
    public string WorkingFolder { get; init; } = Directory.GetCurrentDirectory();
}

/// <summary>
///  runs files-verify and returns the exit code.
/// </summary>
/// <remarks>
///  exit codes: 0 clean, 1 errors (or warnings with --warnings-as-errors),
///  2 the tool itself failed (folder not found, bad arguments, git failed).
/// </remarks>
public class FilesVerifyRunner
{
    public const int ExitClean = 0;
    public const int ExitProblems = 1;
    public const int ExitToolFailed = 2;

    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly Func<string, string?> _getEnvironment;

    public FilesVerifyRunner(TextWriter output, TextWriter error, Func<string, string?>? getEnvironment = null)
    {
        _output = output;
        _error = error;
        _getEnvironment = getEnvironment ?? Environment.GetEnvironmentVariable;
    }

    public int Run(FilesVerifySettings settings)
    {
        try
        {
            return RunChecks(settings);
        }
        catch (Exception ex) when (ex is SyncFolderNotFoundException or GitException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            _error.WriteLine($"ERROR: {ex.Message}");
            return ExitToolFailed;
        }
    }

    private int RunChecks(FilesVerifySettings settings)
    {
        var format = GetFormat(settings.Format);

        var location = string.IsNullOrWhiteSpace(settings.Folder)
            ? SyncFolderLocator.Discover(settings.WorkingFolder)
            : SyncFolderLocator.FromFolder(Path.Combine(settings.WorkingFolder, settings.Folder));

        var result = new SyncFolderVerifier().Verify(location.Folder, new SyncFolderVerifyOptions
        {
            IncludeContent = settings.Content
        });

        var issues = result.Issues.ToList();

        IReadOnlyList<GitChange>? changes = null;
        if (string.IsNullOrWhiteSpace(settings.ChangedSince) is false)
        {
            changes = GitChanges.GetChanges(location.Folder, settings.ChangedSince);
            issues = FilterToChanges(issues, changes);
        }

        if (string.IsNullOrWhiteSpace(settings.PublishFolder) is false)
        {
            issues.AddRange(SyncPublishFolderCheck.Check(location.Folder, location.SiteRoot,
                Path.Combine(settings.WorkingFolder, settings.PublishFolder)));
        }

        var report = new FilesVerifyReport
        {
            Folder = location.Folder,
            BaseFolder = GetBaseFolder(location.Folder, settings.WorkingFolder),
            Issues = issues,
            FileCount = result.FileCount,
            SkippedFolders = result.SkippedFolders,
            WarningsAsErrors = settings.WarningsAsErrors,
        };

        WriteReport(format, settings.Output, report);

        if (settings.Summary)
        {
            var itemChanges = changes is null
                ? null
                : FilesVerifySummary.GetItemChanges(location.Folder, changes, settings.ChangedSince!);

            var markdown = FilesVerifySummary.Build(report, changes is null ? null : settings.ChangedSince, itemChanges);
            if (StepSummary.TryAppend(markdown, _getEnvironment) is false)
            {
                // with sarif/junit going to stdout, keep stdout clean.
                var writer = IsDocumentOnStdout(format, settings.Output) ? _error : _output;
                writer.WriteLine();
                writer.Write(markdown);
            }
        }

        return report.Failed ? ExitProblems : ExitClean;
    }

    private string GetFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return string.Equals(_getEnvironment("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                ? FilesVerifyFormats.GitHub
                : FilesVerifyFormats.Text;
        }

        var value = format.Trim().ToLowerInvariant();
        if (FilesVerifyFormats.All.Contains(value) is false)
            throw new ArgumentException($"Unknown format '{format}', use one of {string.Join(", ", FilesVerifyFormats.All)}.");
        return value;
    }

    private static bool IsDocumentOnStdout(string format, string? output)
        => format is FilesVerifyFormats.Sarif or FilesVerifyFormats.JUnit && string.IsNullOrWhiteSpace(output);

    private void WriteReport(string format, string? output, FilesVerifyReport report)
    {
        if (format is FilesVerifyFormats.Sarif or FilesVerifyFormats.JUnit)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                FilesVerifyFormats.Write(format, report, _output);
                _error.WriteLine(report.SummaryLine());
                return;
            }

            var path = Path.GetFullPath(output);
            var folder = Path.GetDirectoryName(path);
            if (folder is not null) Directory.CreateDirectory(folder);
            using (var file = new StreamWriter(path))
            {
                FilesVerifyFormats.Write(format, report, file);
            }

            // still show the problems in the log.
            var logFormat = string.Equals(_getEnvironment("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                ? FilesVerifyFormats.GitHub
                : FilesVerifyFormats.Text;
            FilesVerifyFormats.Write(logFormat, report, _output);
            _output.WriteLine($"{format} written to {path}");
            return;
        }

        if (string.IsNullOrWhiteSpace(output) is false)
        {
            using var file = new StreamWriter(Path.GetFullPath(output));
            FilesVerifyFormats.Write(format, report, file);
        }

        FilesVerifyFormats.Write(format, report, _output);
    }

    /// <summary>
    ///  only keep problems in files changed on the branch. Every file was still
    ///  loaded, so a clash between a changed file and an unchanged one is kept.
    /// </summary>
    public static List<SyncFileIssue> FilterToChanges(IEnumerable<SyncFileIssue> issues, IEnumerable<GitChange> changes)
    {
        var changed = changes.Select(x => x.FilePath).ToHashSet(GitChanges.PathComparer);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return issues
            .Where(issue =>
            {
                var path = Path.GetFullPath(issue.FilePath);
                if (changed.Contains(path)) return true;

                // folder problems: keep if anything in the folder changed.
                var folder = Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar;
                return Directory.Exists(path) && changed.Any(x => x.StartsWith(folder, comparison));
            })
            .ToList();
    }

    /// <summary>
    ///  paths in the output are relative to the GitHub workspace or repo root,
    ///  so annotations and SARIF results point at the right file.
    /// </summary>
    private string GetBaseFolder(string folder, string workingFolder)
    {
        var workspace = _getEnvironment("GITHUB_WORKSPACE");
        if (string.IsNullOrWhiteSpace(workspace) is false && Directory.Exists(workspace))
            return Path.GetFullPath(workspace);

        return GitChanges.GetTopLevel(folder) ?? Path.GetFullPath(workingFolder);
    }
}
