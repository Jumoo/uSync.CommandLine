using System.CommandLine;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using uSync.Commands.Core;

namespace uSync.Handlers;
internal class RunCommandHandler : RemoteCommandHandlerBase
{
    private readonly TextWriter _writer;

    public RunCommandHandler(ILogger logger, TextWriter writer) : base(logger)
    {
        _writer = writer;

        var argumentCommand = new Argument<string>(
            "command", "Command to issue to server");

        var optionParameters = new Option<IEnumerable<string>>(
            new[] { "--parameters", "-p" }, "Parameters to pass to server command")
        {
            AllowMultipleArgumentsPerToken = true
        };

        Command = new Command("run", "Run a command against a server")
        {
            optionServerName,
            argumentCommand,
            optionParameters,
            optionAuthKey,
            optionUsername,
            optionPassword
        };

        Command.SetHandler(async (context) =>
        {
            var parameters = new RunCommandParameters
            {
                ServerName = context.ParseResult?.GetValueForOption(optionServerName),
                Command = context.ParseResult?.GetValueForArgument(argumentCommand),
                Parameters = context.ParseResult?.GetValueForOption(optionParameters),

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
                context.ExitCode = 1;
            }
        });

    }

    private async Task<int> InvokeCommand(RunCommandParameters parameters)
    {
        var runtimeService = EnsureRuntimeService(parameters);

        if (string.IsNullOrEmpty(parameters.Command))
            throw new uSyncCommandException(14, "Missing command name argument");

        await _writer.WriteLineAsync($"Running {runtimeService.Uri} [{parameters.Command}]");

        var result = await runtimeService.ExecuteCommandAsync(parameters?.Command, parameters?.Parameters, _writer);
        if (result == null)
        {
            _logger.LogWarning("No result received");
            return -1;
        }

        if (result.Success)
        {
            await OutputResultAsync(result);
        }
        else
        {
            _logger.LogError($"ERROR: {result.Message}");
            return -1;
        }

        return 0;
    }

    private async Task OutputResultAsync(SyncCommandResponse response)
    {
        switch (response.ResultType)
        {
            case SyncCommandObjectType.Json:
                var result = JsonConvert.SerializeObject(response.Result, Formatting.Indented);
                await _writer.WriteLineAsync($"Result: {result}");
                break;
            default:
                await _writer.WriteLineAsync($"Result: {response.Result}");
                break;
        }
    }


    private class RunCommandParameters : RemoteCommandParameters
    {
        public string? Command { get; init; }
        public IEnumerable<string>? Parameters { get; init; }
    }
}
