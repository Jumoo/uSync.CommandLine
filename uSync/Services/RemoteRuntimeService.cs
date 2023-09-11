using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using uSync.Auth;
using uSync.Commands.Core;

using Umbraco.Extensions;

namespace uSync.Services;
internal class RemoteRuntimeService : IRemoteRuntimeService
{
    private ILogger _logger;

    public Uri Uri { get; private set; }

    private readonly string? _key;
    private readonly string? _username;
    private readonly string? _password;

    private RemoteRuntimeService(ILogger logger, Uri uri)
    {
        _logger = logger;
        this.Uri = new Uri(uri.AbsoluteUri.EnsureEndsWith("/"), UriKind.Absolute);
    }

    public RemoteRuntimeService(ILogger logger, Uri uri, string? username, string? password)
        : this(logger, uri)
    {
        _username = username;
        _password = password;
    }


    public RemoteRuntimeService(ILogger logger, Uri uri, string key)
        : this(logger, uri)
    {
        _key = key;
    }

    public async Task<IReadOnlyCollection<SyncCommandInfo>> ListCommandsAsync(string? commandName)
    {
        using (var client = GetHttpClient())
        {
            var url = GetActionUrl("List");

            if (!string.IsNullOrWhiteSpace(commandName))
                url += $"?command={commandName}";

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            try
            {
                return JsonConvert.DeserializeObject<IReadOnlyCollection<SyncCommandInfo>>(content)
                    ?? new List<SyncCommandInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                _logger.LogError(content);
                return new List<SyncCommandInfo>();
            }
        }
    }

    public async Task<SyncCommandResponse?> ExecuteCommandAsync(string? command,
        IEnumerable<string>? parameters,
        TextWriter writer)
    {
        var page = 0;
        var stepIndex = 0;
        var actionAlias = string.Empty;

        var request = new SyncCommandRequest
        {
            Id = Guid.NewGuid(),
            CommandId = command,
            Page = page,
            StepIndex = stepIndex,
            ActionId = actionAlias,
            Parameters = GetParameters(parameters),
            PageSize = 15,
        };

        bool isComplete = false;
        bool hasError = false;
        SyncCommandResponse? response = default;

        while (!isComplete && !hasError)
        {
            request.Page = page;

            await writer.WriteAsync($"{request.CommandId, -10} : {page,3} > ");

            response = await InvokeRemoteCommandAsync(request);

            if (response == null)
            {
                return new SyncCommandResponse(request.Id)
                {
                    Success = false,
                    Message = "Error - Null response received"
                };
            }

            isComplete = response.Complete;
            hasError = !response.Success;

            if (!hasError)
            {
                await writer.WriteLineAsync($"{response.Success,-6} {response.Message}");

                if (response.AdditionalData.Count > 0)
                {
                    _logger.LogDebug(">>> Data: {data}", string.Join(",", response.AdditionalData
                        .Select(x => $"[{x.Key}={x.Value}]")));
                }
            }

            page++;

            if (!isComplete)
            {
                // if this is a new step reset the page count;
                if (response.ResetPaging 
                    || request.StepIndex != response.StepIndex
                    || request.ActionId != response.NextAction)
                {
                    page = 0;
                }

                request.AdditionalData = response.AdditionalData;

                request.StepIndex = response.StepIndex;
                request.ActionId = response.NextAction;
            }
            else
            {
                await writer.WriteLineAsync("## Completed ##");
            }
        }

        return response;
    }


    private async Task<SyncCommandResponse?> InvokeRemoteCommandAsync(SyncCommandRequest request)
    {
        using (var client = GetHttpClient())
        {
            var url = GetActionUrl("Execute");

            _logger.LogDebug("Invoke: {url} {requestId} {action}-{step}-{page}",
                url, request.Id, request.ActionId, request.StepIndex, request.Page);

            if (request.AdditionalData.Count > 0)
            {
                _logger.LogDebug("Invoke: Additional {data}", string.Join(",", request.AdditionalData
                        .Select(x => $"[{x.Key}={x.Value}]")));
            }

            var response = await client.PostAsJsonAsync(url, request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonConvert.DeserializeObject<SyncCommandResponse>(content) ?? default;

            return new SyncCommandResponse(request.Id)
            {
                Success = false,
                Message = $"Remote error: [{url}] {response.StatusCode} {content}"
            };
        }
    }


    private string GetActionUrl(string command)
        => $"uSync/SyncCommand/{command}";


    private IReadOnlyCollection<SyncCommandParameter> GetParameters(IEnumerable<string>? parameters)
    {
        if (parameters == null)
            return new List<SyncCommandParameter>();

        var parameterList = new List<SyncCommandParameter>();

        foreach (var parameter in parameters)
        {
            var arguments = parameter.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);

            if (arguments.Length == 2)
            {
                parameterList.Add(new SyncCommandParameter
                {
                    Id = arguments[0],
                    Value = arguments[1]
                });
            }
            else if (arguments.Length == 1)
            {
                parameterList.Add(new SyncCommandParameter { Id = arguments[0] });
            }
        }

        return parameterList;
    }

    private HttpClient GetHttpClient()
    {
        if (!string.IsNullOrWhiteSpace(_key))
            return GetHmacClient(Uri, _key);

        return GetBasicClient(Uri, _username, _password);
    }

    private HttpClient GetBasicClient(Uri uri, string? username, string? password)
         => GetBaseClient(uri, new BasicHandler(username, password));

    private HttpClient GetHmacClient(Uri uri, string key)
        => GetBaseClient(uri, new HmacHandler(key));

    private HttpClient GetBaseClient(Uri uri, DelegatingHandler handler)
    {
        var client = HttpClientFactory.Create(handler);
        client.BaseAddress = uri;
        return client;
    }
}
