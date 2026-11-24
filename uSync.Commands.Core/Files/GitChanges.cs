using System.Diagnostics;

namespace uSync.Commands.Core.Files;

public enum GitChangeKind
{
    Added,
    Changed,
    Deleted
}

/// <summary>
///  a file changed on this branch, with its full path.
/// </summary>
public sealed record GitChange(string FilePath, GitChangeKind Kind);

/// <summary>
///  asks git what has changed since a ref.
/// </summary>
public static class GitChanges
{
    /// <summary>
    ///  the repository's top folder, or null if the folder isn't in a git repository.
    /// </summary>
    public static string? GetTopLevel(string folder)
    {
        try
        {
            var top = Run(folder, "rev-parse", "--show-toplevel").Trim();
            return string.IsNullOrWhiteSpace(top) ? null : Path.GetFullPath(top);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///  files changed since the ref: committed on this branch
    ///  (git diff ref...HEAD), uncommitted, and untracked.
    /// </summary>
    /// <exception cref="GitException">git isn't there, the folder isn't a repo, or the ref is unknown.</exception>
    public static IReadOnlyList<GitChange> GetChanges(string folder, string gitRef)
    {
        if (string.IsNullOrWhiteSpace(gitRef) || gitRef.StartsWith('-'))
            throw new GitException($"'{gitRef}' is not a valid git ref.");

        var top = GetTopLevel(folder)
            ?? throw new GitException($"{folder} is not in a git repository, so --changed-since can't be used.");

        var changes = new Dictionary<string, GitChangeKind>(PathComparer);

        // committed on this branch since it left the ref.
        Add(changes, top, Run(top, "diff", "--name-status", "--no-renames", "-z", $"{gitRef}...HEAD", "--"));

        // not committed yet (staged or not), so a local run matches what will be pushed.
        Add(changes, top, Run(top, "diff", "--name-status", "--no-renames", "-z", "HEAD", "--"));

        foreach (var path in Run(top, "ls-files", "--others", "--exclude-standard", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries))
            changes[ToFullPath(top, path)] = GitChangeKind.Added;

        return changes.Select(x => new GitChange(x.Key, x.Value)).ToList();
    }

    /// <summary>
    ///  the content of a file at a ref (e.g. a deleted file), or null.
    /// </summary>
    public static string? GetFileAtRef(string filePath, string gitRef)
    {
        if (gitRef.StartsWith('-')) return null;
        var folder = Path.GetDirectoryName(filePath);
        while (folder is not null && Directory.Exists(folder) is false)
            folder = Path.GetDirectoryName(folder);

        if (folder is null) return null;
        var top = GetTopLevel(folder);
        if (top is null) return null;

        try
        {
            var relative = Path.GetRelativePath(top, filePath).Replace('\\', '/');
            return Run(top, "show", $"{gitRef}:{relative}");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///  when the file was last committed, or null (not committed, or not in a git repository).
    /// </summary>
    public static DateTimeOffset? GetLastCommitDate(string filePath)
    {
        var folder = Path.GetDirectoryName(filePath);
        if (folder is null) return null;
        var top = GetTopLevel(folder);
        if (top is null) return null;

        try
        {
            var relative = Path.GetRelativePath(top, filePath).Replace('\\', '/');
            var date = Run(top, "log", "-1", "--format=%cI", "--", relative).Trim();
            return DateTimeOffset.TryParse(date, out var value) ? value : null;
        }
        catch (GitException)
        {
            return null;
        }
    }

    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void Add(Dictionary<string, GitChangeKind> changes, string top, string output)
    {
        // -z output is "status\0path\0status\0path\0..."
        var parts = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var n = 0; n + 1 < parts.Length; n += 2)
        {
            var kind = parts[n].Trim() switch
            {
                var s when s.StartsWith('A') => GitChangeKind.Added,
                var s when s.StartsWith('D') => GitChangeKind.Deleted,
                _ => GitChangeKind.Changed
            };

            var path = ToFullPath(top, parts[n + 1]);

            // added on the branch and then changed locally is still added.
            if (kind == GitChangeKind.Changed && changes.TryGetValue(path, out var existing) && existing == GitChangeKind.Added)
                continue;

            changes[path] = kind;
        }
    }

    private static string ToFullPath(string top, string path)
        => Path.GetFullPath(Path.Combine(top, path.Replace('/', Path.DirectorySeparatorChar)));

    private static string Run(string folder, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // stop git asking for anything, and quoting non-ascii paths.
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.quotepath=off");
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new GitException("Could not start git.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new GitException($"Could not run git ({ex.Message}). Is it installed and on the PATH?");
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = stderr.Result.Trim();
                var hint = error.Contains("no merge base", StringComparison.OrdinalIgnoreCase)
                        || error.Contains("unknown revision", StringComparison.OrdinalIgnoreCase)
                        || error.Contains("bad revision", StringComparison.OrdinalIgnoreCase)
                    ? " (in CI, check the checkout fetched the history and the ref, e.g. fetch-depth: 0)"
                    : "";
                throw new GitException($"git {string.Join(' ', args)} failed: {error}{hint}");
            }

            return stdout.Result;
        }
    }
}

public class GitException(string message) : Exception(message);
