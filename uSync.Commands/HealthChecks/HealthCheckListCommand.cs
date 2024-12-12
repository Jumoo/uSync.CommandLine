using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.HealthChecks;

public class HealthCheckListCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }


    public HealthCheckListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("healthcheck-list", "List the health checks on the server");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);

            var healthChecks = await umbracoClient.GetHealthCheckGroupAsync(0, 1000);
            await writer.WriteLineAsync($"Health Check Count: {healthChecks.Total}");
            foreach (var healthCheck in healthChecks.Items)
            {
                await writer.WriteLineAsync($"Health Check: {healthCheck.Name}");
            }
            context.ExitCode = 0;
        });
    }
}
