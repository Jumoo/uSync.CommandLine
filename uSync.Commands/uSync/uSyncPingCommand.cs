using IdentityModel.Client;

using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Http;

namespace uSync.Commands.uSync;
public class uSyncPingCommand : ConnectedCommandBase, ISyncCommand
{
    protected Option<int?> Retrys = new Option<int?>(new string[] { "--retrys", "-r" }, "Number of times to retry the ping (default is 10)");

    public Command Command { get; }
    public uSyncPingCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("usync-ping", "Ping the uSync server")
        {
            Retrys
        };
        AddCoreOptions(Command);
        
        Command.SetHandler(async (context) =>
        {
            var retries = context.ParseResult.GetValueForOption<int?>(Retrys) ?? 10;
            var count = 0;

            var auth = GetConnectionPartameters(context);
            client.BaseAddress = auth.Url;

            await Writer.WriteLineAsync($"Pinging {auth.Url}");

            do {
                count++;
                await Writer.WriteAsync(".");
                try
                {
                    await AttemptConnection(auth);
                    break;
                }
                catch
                {
                    // await Writer.WriteLineAsync(ex.Message);
                }
            } while (count < retries);

            if (count == retries)
            {
                await Writer.WriteLineAsync("Failed to ping server");
            }
            else
            {
                await Writer.WriteLineAsync("Server responded - backoffice now active");
            }

            context.ExitCode = count == retries ? 1 : 0;
        });
    }


    private async Task AttemptConnection(SyncConnectionParameters auth)
    {
        var address = $"{Client.BaseAddress}umbraco/management/api/v1/security/back-office/token";
        var tokenResponse = await Client.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = address,
                ClientId = auth.ClientId,
                ClientSecret = auth.ClientSecret,
            });

        if (tokenResponse.IsError || tokenResponse.AccessToken is null)
        {
            throw new Exception("Error:" + tokenResponse.Error);
        }
    }
}
