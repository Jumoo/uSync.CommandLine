using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace uSync.Commands.Core.Output;

/// <summary>
///  --json for the list commands, so scripts can read the result
///  (jq, ConvertFrom-Json) instead of scraping the text.
/// </summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Option<bool> CreateOption()
        => new("--json", "write the result as JSON (the banner goes to stderr, so stdout is only the JSON)");

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);

    public static Task WriteAsync<T>(TextWriter writer, T value)
        => writer.WriteLineAsync(Serialize(value));
}
