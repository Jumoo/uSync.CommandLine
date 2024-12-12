using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.HealthChecks;

public class HealthCheckGroupListCommand : ConnectedCommandBase, ISyncCommand
{
    protected Option<string> indexName = new Option<string>(new string[] { "--group", "-g" }, "Group name of health check group to list");
    public Command Command { get; }
    public HealthCheckGroupListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("healthcheck-group-list", "List the health check groups on the server");
        AddCoreOptions(Command);
        Command.AddOption(indexName);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);

            var groupName = context.ParseResult.GetValueForOption(this.indexName) ??
                throw new Exception("No group-name given");

            var healthCheckGroup = await umbracoClient.GetHealthCheckGroupByNameAsync(groupName);

            await writer.WriteLineAsync($"Health Check Group Count: {healthCheckGroup.Name}");

            foreach(var check in healthCheckGroup.Checks)
            {
                await writer.WriteLineAsync($" [{check.Name}] : {check.Description}");
            }

            context.ExitCode = 0;
        });
    }
}
