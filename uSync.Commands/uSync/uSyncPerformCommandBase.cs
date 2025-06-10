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
        var uSyncClient = await GetUSyncClient(context);

        var auth = GetConnectionPartameters(context);
        var connectionid = await CreateSignalRClient(auth.Url.ToString());
        request.Options.ClientId = connectionid;

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

    protected async Task DisplayResults(PerformActionResponse results)
    {
        // display some results. 
        var changes = results.Status.Sum(x => x.Changes);
        await Writer.WriteLineAsync($"Imported : {changes} changes");

        foreach (var item in results.Status)
        {
            await Writer.WriteLineAsync($"   {item.Name,-20} : {item.Changes}");
        }
    }

    protected async Task<string?> CreateSignalRClient(string host)
    {
        var url = new Uri($"{host}umbraco/SyncHub");
        var connection = new HubConnectionBuilder()
            .WithUrl(url)
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
}
