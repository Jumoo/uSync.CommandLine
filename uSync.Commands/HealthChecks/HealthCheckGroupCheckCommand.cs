using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.HealthChecks;

public class HealthCheckGroupCheckCommand : ConnectedCommandBase, ISyncCommand
{
    protected Option<string> indexName = new Option<string>(new string[] { "--group", "-g" }, "Group name of health check group to list");
    public Command Command { get; }
    public HealthCheckGroupCheckCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("healthcheck-group-check", "Run a health check on the server");
        AddCoreOptions(Command);
        Command.AddOption(indexName);
        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var groupName = context.ParseResult.GetValueForOption(this.indexName) ??
                throw new Exception("No group-name given");

            var healthCheckGroup = await umbracoClient.PostHealthCheckGroupByNameCheckAsync(groupName);

            foreach (var check in healthCheckGroup.Checks)
            {
                await writer.WriteLineAsync($" [{check.Id}]");

                foreach (var result in check.Results)
                {
                    await writer.WriteLineAsync($"  {result.ResultType} : {result.Message}");
                }
            }

            context.ExitCode = 0;
        });
    }
}