using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.CommandLine.Invocation;

using uSync.Management.Api;

namespace uSync.Commands.uSync;

public abstract class uSyncPerformCommandBase : ConnectedCommandBase
{
    protected Option<string> set = new Option<string>(["--set", "-t"], "handler set to use (default : default)");
    protected Option<string> group = new Option<string>(["--group", "-g"], "handler group to use (default : all)");

    public static string[] Dots = [ "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    protected uSyncPerformCommandBase(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    { }

    protected void Spinner(int count)
    {
        if (count % 10 == 0) 
            Writer.Write(".");
    }

    protected async Task<PerformActionResponse> Process(InvocationContext context, PerformActionRequest request)
    {
        var token = await GetToken(context);
        var uSyncClient = await GetUSyncClient(context, token);

        var auth = GetConnectionPartameters(context);
        var connectionId = await CreateSignalRClient(auth.Url.ToString(), token);
        request.Options.ClientId = connectionId;

        PerformActionResponse response;
        int count = 0;
        do
        {
            count++;
            response = await uSyncClient.PerformActionAsync(request);

            Spinner(count);

            var current = response.Status.FirstOrDefault(x => x.Status == HandlerStatus.Processing);
            request.StepNumber = count;

        } while (response.Complete is false && count < 100);

        return response;
    }

    protected async Task DisplayResults(string action, PerformActionResponse results)
    {
        // display some results. 
        var changes = results.Status.Sum(x => x.Changes);
        await Writer.WriteLineAsync($"{action} : {changes} changes");

        foreach (var item in results.Status)
        {
            await Writer.WriteLineAsync($"   {item.Name,-20} : {item.Changes}");
        }
    }

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
