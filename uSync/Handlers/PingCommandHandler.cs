using System.CommandLine;

using Microsoft.Extensions.Logging;

namespace uSync.Handlers;
internal class PingCommandHandler : RemoteCommandHandlerBase
{
    private TextWriter _writer;

    public PingCommandHandler(ILogger logger, TextWriter writer) : base(logger)
    {
        _writer = writer;

        var timeoutCommand = new Option<int>("-w", "Timeout wait for each reply");

        Command = new Command("ping", "Ping a server until Umbraco is responding")
        {
            optionServerName,
            timeoutCommand,
            optionAuthKey,
            optionUsername,
            optionPassword
        };

        Command.SetHandler(async (context) =>
        {
            var parameters = new PingCommandParameters
            {
                ServerName = context.ParseResult?.GetValueForOption(optionServerName),
                TimeoutWait = context.ParseResult?.GetValueForOption(timeoutCommand),
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

    private async Task<int> InvokeCommand(PingCommandParameters parameters)
    {
        int retrys = 10;

        var runtimeService = EnsureRuntimeService(parameters);

        for (int n = 0; n < retrys; n++)
        {
            try
            {
                await _writer.WriteAsync($"{n + 1}...");
                var result = await runtimeService.ExecuteCommandAsync("Ping", Enumerable.Empty<string>(), _writer);

                if (result == null || !result.Success)
                    throw new Exception($"Failed {result?.Message ?? "NULL"}");

                await _writer.WriteAsync($"Successfully connected to {runtimeService.Uri}");

                return 0;
            }
            catch (Exception ex)
            {
                await _writer.WriteLineAsync($"Timeout {ex.Message}");
                Thread.Sleep(500);
            }
        }

        await _writer.WriteLineAsync("Failed to connect to server");
        return -1;
    }


    private class PingCommandParameters : RemoteCommandParameters
    {
        public int? TimeoutWait { get; init; }
    }

}
