using System.Globalization;
using System.Text.RegularExpressions;

namespace uSync.Commands.LogViewer;

/// <summary>
///  reads a --since value: a duration back from now (90s, 15m, 2h, 1d) or a date/time.
/// </summary>
public static partial class LogSince
{
    [GeneratedRegex(@"^\s*(\d+)\s*(s|m|h|d)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPattern();

    public static bool TryParse(string? value, DateTimeOffset now, out DateTimeOffset since)
    {
        since = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var match = DurationPattern().Match(value);
        if (match.Success)
        {
            var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var span = char.ToLowerInvariant(match.Groups[2].Value[0]) switch
            {
                's' => TimeSpan.FromSeconds(amount),
                'm' => TimeSpan.FromMinutes(amount),
                'h' => TimeSpan.FromHours(amount),
                _ => TimeSpan.FromDays(amount),
            };
            since = now - span;
            return true;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out since);
    }
}
