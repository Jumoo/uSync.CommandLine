namespace uSync.Commands.Core.Output;

/// <summary>
///  GitHub Actions job summary ($GITHUB_STEP_SUMMARY).
/// </summary>
public static class StepSummary
{
    public const string EnvironmentVariable = "GITHUB_STEP_SUMMARY";

    /// <summary>
    ///  append markdown to the step summary, when the variable is set.
    /// </summary>
    /// <returns>true if it was written.</returns>
    public static bool TryAppend(string markdown, Func<string, string?>? getEnvironment = null)
    {
        var path = (getEnvironment ?? Environment.GetEnvironmentVariable)(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path)) return false;

        File.AppendAllText(path, markdown.EndsWith('\n') ? markdown : markdown + Environment.NewLine);
        return true;
    }

    /// <summary>
    ///  make a value safe to put in a markdown table cell.
    /// </summary>
    public static string Cell(string? value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
