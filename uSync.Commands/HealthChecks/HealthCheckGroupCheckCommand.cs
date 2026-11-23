using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.HealthChecks;

public class HealthCheckGroupCheckCommand : ConnectedCommandBase, ISyncCommand
{
    protected Option<string> indexName = new Option<string>(new string[] { "--group", "-g" }, "Group name of health check group to list") { IsRequired = true };
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
                throw new SyncUsageException("No group given, use --group");

            var healthCheckGroup = await umbracoClient.PostHealthCheckGroupByNameCheckAsync(groupName);

            var errors = 0;
            foreach (var check in healthCheckGroup.Checks)
            {
                await writer.WriteLineAsync($" [{check.Id}]");

                foreach (var result in check.Results ?? [])
                {
                    if (result.ResultType == Umbraco.Management.Api.StatusResultTypeModel.Error) errors++;
                    await writer.WriteLineAsync($"  {result.ResultType} : {result.Message}");
                }
            }

            // non-zero on any error, so a pipeline step fails.
            if (errors > 0)
                await writer.WriteLineAsync($"{errors} health check result(s) in '{groupName}' are errors.");

            context.ExitCode = errors > 0 ? 1 : 0;
        });
    }
}