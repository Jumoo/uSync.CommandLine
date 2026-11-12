using System.Text.Json.Serialization;

namespace uSync.Management.Api;

// Hand written additions for background runs (uSync 17.4.0+). The generated
// client was built against an older site, before the server returned an
// operation id or had a Status endpoint. Regenerating would replace these.

public partial class PerformActionResponse
{
    /// <summary>
    ///  id of the long-running operation, when the server is running the
    ///  action in the background.
    /// </summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; set; }

    /// <summary>
    ///  set when the server could not start the action (e.g. a background
    ///  run of the same action is already going).
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
///  status of a background run, from umbraco/usync/api/v1/Status.
/// </summary>
public class SyncOperationStatusResponse
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>
    ///  Enqueued, Running, Success, Failed, Stale or NotFound.
    /// </summary>
    [JsonPropertyName("operationStatus")]
    public string OperationStatus { get; set; } = string.Empty;

    [JsonPropertyName("complete")]
    public bool Complete { get; set; }

    [JsonPropertyName("status")]
    public ICollection<SyncHandlerSummary>? Status { get; set; }

    [JsonPropertyName("actions")]
    public ICollection<USyncActionView>? Actions { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public partial class uSyncClient
{
    /// <summary>
    ///  get the status of a background run.
    /// </summary>
    public virtual async Task<SyncOperationStatusResponse> GetOperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}umbraco/usync/api/v1/Status?operationId={Uri.EscapeDataString(operationId)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var status = (int)response.StatusCode;
        if (status != 200)
        {
            var headers = response.Headers.ToDictionary(x => x.Key, x => x.Value);
            throw new ApiException($"The HTTP status code of the response was not expected ({status}).", status, body, headers, null);
        }

        return System.Text.Json.JsonSerializer.Deserialize<SyncOperationStatusResponse>(body, JsonSerializerSettings)
            ?? throw new ApiException("Response was null which was not expected.", status, body, new Dictionary<string, IEnumerable<string>>(), null);
    }
}
