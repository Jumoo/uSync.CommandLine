using IdentityModel.Client;

using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.Diagnostics;
using System.Net;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Http;

namespace uSync.Commands.uSync;

/// <summary>
///  wait for a site to come up (e.g. after a deploy).
/// </summary>
/// <remarks>
///  the site is "up" when it hands out an access token for the API user.
/// </remarks>
public class uSyncPingCommand : ConnectedCommandBase, ISyncCommand
{
    protected Option<int?> Retrys = new Option<int?>(new string[] { "--retrys", "-r" }, "Maximum number of attempts (default: keep trying until --timeout)");
    protected Option<int?> Timeout = new Option<int?>(new string[] { "--timeout" }, "Seconds to wait for the site to answer before giving up (default 300)");

    public const int DefaultTimeoutSeconds = 300;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxAttemptTime = TimeSpan.FromSeconds(30);

    public Command Command { get; }
    public uSyncPingCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("usync-ping", "Wait for the site to answer (e.g. after a deploy)")
        {
            Retrys,
            Timeout
        };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var retries = context.ParseResult.GetValueForOption(Retrys);
            var timeoutSeconds = context.ParseResult.GetValueForOption(Timeout) ?? DefaultTimeoutSeconds;
            if (timeoutSeconds <= 0) timeoutSeconds = DefaultTimeoutSeconds;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            var auth = GetConnectionPartameters(context);
            client.BaseAddress ??= auth.Url;

            await Writer.WriteLineAsync($"Pinging {auth.Url} (timeout {timeoutSeconds}s)");

            var cancel = context.GetCancellationToken();
            var elapsed = Stopwatch.StartNew();
            var attempts = 0;
            string? lastError = null;

            while (cancel.IsCancellationRequested is false)
            {
                attempts++;
                await Writer.WriteAsync(".");

                var result = await AttemptConnection(auth, Min(MaxAttemptTime, timeout - elapsed.Elapsed), cancel);
                if (result.Connected)
                {
                    await Writer.WriteLineAsync();
                    await Writer.WriteLineAsync($"Server responded - backoffice now active ({elapsed.Elapsed.TotalSeconds:0}s)");
                    context.ExitCode = 0;
                    return;
                }

                if (result.Fatal)
                {
                    // the site answered, but won't give us a token - waiting won't help.
                    await Writer.WriteLineAsync();
                    await Writer.WriteLineAsync($"ERROR: {result.Error}");
                    context.ExitCode = 1;
                    return;
                }

                lastError = result.Error;

                if (retries.HasValue && attempts >= retries.Value) break;
                var remaining = timeout - elapsed.Elapsed;
                if (remaining <= TimeSpan.FromSeconds(1)) break;

                try { await Task.Delay(remaining < RetryDelay ? remaining - TimeSpan.FromSeconds(1) : RetryDelay, cancel); }
                catch (TaskCanceledException) { break; }
            }

            await Writer.WriteLineAsync();
            await Writer.WriteLineAsync($"ERROR: the site never came up - no answer from {auth.Url} after {elapsed.Elapsed.TotalSeconds:0}s ({attempts} attempts).");
            if (string.IsNullOrWhiteSpace(lastError) is false)
                await Writer.WriteLineAsync($"   last error: {lastError}");

            context.ExitCode = 1;
        });
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b)
    {
        var value = a < b ? a : b;
        return value < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : value;
    }

    private record PingResult(bool Connected, bool Fatal, string? Error);

    private async Task<PingResult> AttemptConnection(SyncConnectionParameters auth, TimeSpan attemptTimeout, CancellationToken cancel)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        cts.CancelAfter(attemptTimeout);

        try
        {
            var address = $"{auth.Url}umbraco/management/api/v1/security/back-office/token";
            var tokenResponse = await Client.RequestClientCredentialsTokenAsync(
                new ClientCredentialsTokenRequest
                {
                    Address = address,
                    ClientId = auth.ClientId,
                    ClientSecret = auth.ClientSecret,
                }, cts.Token);

            if (tokenResponse.IsError is false && tokenResponse.AccessToken is not null)
                return new PingResult(true, false, null);

            // a 400/401/403 from the token endpoint is the site answering,
            // so it is up and the credentials are wrong - waiting won't help.
            if (HttpClientExtensions.IsCredentialRejection(tokenResponse))
                return new PingResult(false, true, HttpClientExtensions.CredentialRejectionMessage(auth, tokenResponse));

            var error = tokenResponse.Error ?? $"{(int)tokenResponse.HttpStatusCode} {tokenResponse.HttpStatusCode}";
            return new PingResult(false, false, error);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested is false)
        {
            return new PingResult(false, false, $"no response within {attemptTimeout.TotalSeconds:0}s");
        }
        catch (Exception ex)
        {
            return new PingResult(false, false, ex.Message);
        }
    }
}
