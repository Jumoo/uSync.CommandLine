using Microsoft.Extensions.Configuration;

using System.CommandLine;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Server;

/// <summary>
///  report the site's runtime level and version, and fail unless it is running.
/// </summary>
/// <remarks>
///  the status endpoint doesn't need a token, so this still answers when the
///  site is waiting for an upgrade or failed to boot (when the API user can't sign in).
/// </remarks>
public class ServerStatusCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public ServerStatusCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("server-status", "Show the site's runtime level and Umbraco version. Exits 1 unless the site is running.");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var auth = GetConnectionPartameters(context);
            Client.BaseAddress ??= auth.Url;
            var cancel = context.GetCancellationToken();

            var anonymous = new UmbracoClient(auth.Url.AbsoluteUri, Client);
            var status = await anonymous.GetServerStatusAsync(cancel);

            await Writer.WriteLineAsync($"Server:  {auth.Url}");
            await Writer.WriteLineAsync($"Status:  {status.ServerStatus}");

            if (status.ServerStatus != RuntimeLevelModel.Run)
            {
                await Writer.WriteLineAsync(Describe(status.ServerStatus));
                context.ExitCode = 1;
                return;
            }

            var umbracoClient = await GetUmbracoClient(context);
            var information = await umbracoClient.GetServerInformationAsync(cancel);
            await Writer.WriteLineAsync($"Version: {information.Version}");
            await Writer.WriteLineAsync($"Mode:    {information.RuntimeMode}");

            context.ExitCode = 0;
        });
    }

    public static string Describe(RuntimeLevelModel level) => level switch
    {
        RuntimeLevelModel.Upgrade => "ERROR: the site is waiting for an upgrade - run it from the backoffice, or set Umbraco:CMS:Unattended:UpgradeUnattended.",
        RuntimeLevelModel.Upgrading => "ERROR: the site is upgrading - try again when it has finished.",
        RuntimeLevelModel.Install => "ERROR: the site is waiting to be installed.",
        RuntimeLevelModel.BootFailed => "ERROR: the site failed to boot - check its logs.",
        _ => $"ERROR: the site isn't running yet ({level})."
    };
}
