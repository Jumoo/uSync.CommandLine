using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

using IdentityModel.Client;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Text.Json;

using uSync.Management.Api;

namespace uSync.Commands.uSync;

public abstract class uSyncPerformCommandBase : ConnectedCommandBase
{
    protected Option<string> set = new Option<string>(["--set", "-t"], "handler set to use (default : the site's default set)");
    protected Option<string> group = new Option<string>(["--group", "-g"], "handler group to use (default : all)");

    // the server works through one handler per request, there are ~20 handlers,
    // so if we go round this many times something has gone wrong.
    private const int MaxSteps = 100;

    private static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromMinutes(2);

    protected uSyncPerformCommandBase(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    { }

    /// <summary>
    ///  build the request for an action.
    /// </summary>
    /// <remarks>
    ///  RequestId is left empty so the server treats the first call as the start
    ///  of a new run (raising the starting notifications and clearing caches),
    ///  and returns the id we then use for every following step.
    ///
    ///  Set is left empty unless given, so the server uses its configured default
    ///  set - set names are case sensitive on the server.
    /// </remarks>
    protected PerformActionRequest CreateRequest(InvocationContext context, string action, bool force = false)
        => new()
        {
            Action = action,
            RequestId = string.Empty,
            Options = new USyncOptions
            {
                Set = context.ParseResult.GetValueForOption(set) ?? string.Empty,
                Group = context.ParseResult.GetValueForOption(group) ?? "all",
                Force = force,
            },
        };

    /// <summary>
    ///  run the action on the server, one handler at a time.
    /// </summary>
    /// <returns>the final response, or null if the run failed part way.</returns>
    protected async Task<PerformActionResponse?> Process(InvocationContext context, PerformActionRequest request)
    {
        var token = await GetToken(context);
        var uSyncClient = await GetUSyncClient(context, token);

        var auth = GetConnectionPartameters(context);
        var connectionId = await CreateSignalRClient(auth.Url.ToString(), token);
        request.Options.ClientId = connectionId ?? string.Empty;

        await Writer.WriteLineAsync($"{request.Action} : set '{DisplayValue(request.Options.Set, "(default)")}', group '{request.Options.Group}'{(request.Options.Force ? ", forced" : "")}");

        PerformActionResponse? response = null;
        var tokenAge = Stopwatch.StartNew();
        while (request.StepNumber < MaxSteps)
        {
            var handlerName = GetHandlerName(response, request.StepNumber);

            try
            {
                // Umbraco access tokens only last 5 minutes by default, and a
                // full import can easily run longer than that.
                if (tokenAge.Elapsed > TokenRefreshInterval)
                {
                    Client.SetBearerToken(await GetToken(context));
                    tokenAge.Restart();
                }

                response = await uSyncClient.PerformActionAsync(request);
            }
            catch (System.Exception ex)
            {
                await WriteStepError(request, handlerName, ex);
                context.ExitCode = 1;
                return null;
            }

            if (response.InBackground)
            {
                await Writer.WriteLineAsync("The server is running this in the background (uSync ProcessingMode is 'Background').");
                await Writer.WriteLineAsync("This command can't follow background runs yet - check the uSync dashboard or the site logs for the results.");
                return response;
            }

            await WriteStepProgress(response, request.StepNumber);

            if (response.Complete) return response;

            request.RequestId = response.RequestId;
            request.StepNumber++;
        }

        await Writer.WriteLineAsync($"ERROR: {request.Action} did not complete after {MaxSteps} steps, stopping.");
        context.ExitCode = 1;
        return null;
    }

    private static string? GetHandlerName(PerformActionResponse? response, int step)
        => response?.Status?.ElementAtOrDefault(step)?.Name;

    private async Task WriteStepProgress(PerformActionResponse response, int step)
    {
        var handlers = response.Status?.ToList() ?? [];
        if (step >= handlers.Count) return; // final (tidy up) step

        var handler = handlers[step];
        var error = handler.InError ? "  (errors)" : "";
        await Writer.WriteLineAsync($"   [{step + 1,2}/{handlers.Count}] {handler.Name,-20} : {handler.Changes}{error}");
    }

    private async Task WriteStepError(PerformActionRequest request, string? handlerName, System.Exception ex)
    {
        var name = handlerName ?? (request.StepNumber == 0 ? "first handler" : "final steps");
        await Writer.WriteLineAsync();
        await Writer.WriteLineAsync($"ERROR: {request.Action} failed on step {request.StepNumber + 1} ({name}).");

        var message = ex switch
        {
            TaskCanceledException => $"The request timed out after {Client.Timeout}. (set uSync:Command:Timeout in appsettings.json to allow longer)",
            ApiException apiEx => GetApiErrorMessage(apiEx),
            _ => ex.Message
        };

        await Writer.WriteLineAsync($"   {message}");
        await Writer.WriteLineAsync($"   Steps after this one did not run. The site log will have the full error (search for '{name}' or 'uSync').");
    }

    /// <summary>
    ///  make the most of what the server sent back with an error.
    /// </summary>
    /// <remarks>
    ///  Umbraco returns ProblemDetails (title / detail) for errors it handles,
    ///  but an unhandled exception comes back as a bare 500 with no body.
    /// </remarks>
    private static string GetApiErrorMessage(ApiException ex)
    {
        var status = ex.StatusCode switch
        {
            401 => "401 (Unauthorized) - the access token was rejected, check the client id and secret",
            403 => "403 (Forbidden) - the API user does not have access to uSync (it needs access to the Settings section)",
            404 => "404 (Not Found) - is uSync installed on the site, and is the server url right?",
            500 => "500 (Internal Server Error)",
            _ => ex.StatusCode.ToString()
        };

        if (string.IsNullOrWhiteSpace(ex.Response))
            return $"The server returned {status} without any details.";

        try
        {
            using var json = JsonDocument.Parse(ex.Response);
            var title = GetString(json.RootElement, "title");
            var detail = GetString(json.RootElement, "detail");
            if (title is not null || detail is not null)
                return $"The server returned {status}: {string.Join(" - ", new[] { title, detail }.Where(x => x is not null))}";
        }
        catch (JsonException)
        {
            // not json, fall through and show the raw response.
        }

        return $"The server returned {status}: {Truncate(ex.Response)}";
    }

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && string.IsNullOrWhiteSpace(value.GetString()) is false
                ? value.GetString() : null;

    protected async Task DisplayResults(string action, PerformActionResponse? results)
    {
        if (results is null || results.InBackground) return;

        var handlers = results.Status ?? [];
        var changes = handlers.Sum(x => x.Changes);
        await Writer.WriteLineAsync($"{action} : {changes} changes");

        var failed = results.Actions?.Where(x => x.Success is false).ToList() ?? [];
        if (failed.Count > 0)
        {
            await Writer.WriteLineAsync($"{failed.Count} item(s) failed:");
            foreach (var item in failed)
            {
                await Writer.WriteLineAsync($"   {item.Handler} : {item.Name} - {item.Message}");
            }
        }
    }

    /// <summary>
    ///  exit code for a completed run - non zero if any handler or item failed.
    /// </summary>
    protected static int GetExitCode(PerformActionResponse? results)
    {
        if (results is null) return 1;
        if (results.InBackground) return 0;
        if (results.Status?.Any(x => x.InError) is true) return 1;
        if (results.Actions?.Any(x => x.Success is false) is true) return 1;
        return 0;
    }

    private static string DisplayValue(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string Truncate(string? value, int length = 500)
        => value is null ? "" : value.Length <= length ? value : value[..length] + "...";

    protected async Task<string?> CreateSignalRClient(string host,string? token)
    {
        try
        {
            var url = new Uri($"{host}umbraco/SyncHub");
            var connection = new HubConnectionBuilder()
                .WithUrl(url, options=>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .Build();

            connection.On<string>("add", async (message) =>
            {
                await Writer.WriteLineAsync($"Add: {message}");
            });

            connection.On<string>("update", async (message) =>
            {
                await Writer.WriteLineAsync($"Update: {message}");
            });
            await connection.StartAsync();
            return connection.ConnectionId;
        }
        catch
        {
            return "";
        }
    }
}
