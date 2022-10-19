using System.CommandLine;

using Microsoft.Extensions.Logging;

namespace uSync.Handlers;
internal class ListCommandHandler : RemoteCommandHandlerBase
{
    private readonly TextWriter _writer;

    public ListCommandHandler(ILogger logger, TextWriter writer) : base(logger)
    {
        _writer = writer;

        var argumentCommand = new Argument<string>(
           name: "command",
           description: "Command to get information about",
           getDefaultValue: () => string.Empty);

        Command = new Command("list", "List server commands")
        {
            optionServerName,
            argumentCommand,
            optionAuthKey,
            optionUsername,
            optionPassword
        };

        Command.SetHandler(async (context) =>
        {
            var parameters = new ListCommandParameters
            {
                ServerName = context.ParseResult?.GetValueForOption(optionServerName),
                CommandName = context.ParseResult?.GetValueForArgument(argumentCommand),

                AuthKey = context.ParseResult?.GetValueForOption(optionAuthKey),
                Username = context.ParseResult?.GetValueForOption(optionUsername),
                Password = context.ParseResult?.GetValueForOption(optionPassword)
            };

            try
            {
                context.ExitCode = await InvokeCommand(parameters);
            }
            catch (uSyncCommandException syncException)
            {
                _logger.LogError(new EventId(syncException.Id), syncException.Message);
                context.ExitCode = syncException.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                context.ExitCode = -1;
            }
        });
    }

    private async Task<int> InvokeCommand(ListCommandParameters parameters)
    {
        var runtimeService = this.EnsureRuntimeService(parameters);

        var remoteCommands = await runtimeService.ListCommandsAsync(parameters.CommandName);
        await _writer.WriteLineAsync($"Remote commands available for {parameters.ServerName} : \n");

        foreach (var command in remoteCommands)
        {
            if (!string.IsNullOrWhiteSpace(parameters.CommandName)
                && command?.Parameters != null
                && command.Parameters.Count > 0)
            {
                await _writer.WriteLineAsync("Command:");
                await _writer.WriteLineAsync($"  {command.Id,-20} : {command.Description}");
                await _writer.WriteLineAsync("");
                await _writer.WriteLineAsync("Usage:");
                await _writer.WriteLineAsync($"  uSync run {command.Id} -p [parameter=value] <...login options...>");
                await _writer.WriteLineAsync("");
                await _writer.WriteLineAsync("Parameters:");

                foreach (var parameter in command.Parameters)
                {
                    await _writer.WriteLineAsync($"    {parameter.Id,-20} {parameter.ParameterType,-7} {parameter.Description}");
                }

                await _writer.WriteLineAsync("");

            }
            else if (command != null)
            {
                await _writer.WriteLineAsync($"  {command.Id,-20} : {command.Description}");
            }
        }

        if (string.IsNullOrWhiteSpace(parameters.CommandName))
        {
            await _writer.WriteLineAsync(
                "\n\rFor specific information on a single command :" +
                "\n\r   > uSync list <command-name> ..." +
                "\n\rTo run a command :" +
                "\n\r   > uSync run <command-name> ...");
        }

        return 0;
    }


    private class ListCommandParameters : RemoteCommandParameters
    {
        public string? CommandName { get; init; }
    }
}
