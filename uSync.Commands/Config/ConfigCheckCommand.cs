using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Config;

/// <summary>
///  show the connection settings and where each came from, then sign in -
///  without running anything on the site.
/// </summary>
public class ConfigCheckCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public ConfigCheckCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("config-check", "Show the server, client id and secret in use and where each came from, then check they sign in");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var connection = DescribeConnection(context);

            await Writer.WriteLineAsync(Line("Server", connection.Server.Value, connection.Server.Source));
            await Writer.WriteLineAsync(Line("Client id", connection.ClientId.Value, connection.ClientId.Source));
            await Writer.WriteLineAsync(Line("Secret", Mask(connection.Secret.Value), connection.Secret.Source));

            if (connection.ServerUrl is null || connection.ClientId.Value is null || connection.Secret.Value is null)
            {
                await Writer.WriteLineAsync();
                await Writer.WriteLineAsync("ERROR: not configured. Use the command line options, the USYNC_ environment variables, or uSync:Command in appsettings.json.");
                context.ExitCode = SyncCommandErrors.ExitUsage;
                return;
            }

            // throws (and the error handler reports it) if the id doesn't look right or the site says no.
            var umbracoClient = await GetUmbracoClient(context);
            var user = await umbracoClient.GetUserCurrentAsync(context.GetCancellationToken());

            await Writer.WriteLineAsync();
            await Writer.WriteLineAsync($"Signed in as: {user.Name} ({user.Email})");
            context.ExitCode = 0;
        });
    }

    private static string Line(string name, string? value, string? source)
        => value is null
            ? $"{name + ":",-11}(not set)"
            : $"{name + ":",-11}{value}  (from {source})";

    /// <summary>
    ///  never print any of the secret (this ends up in build logs), only its length.
    /// </summary>
    public static string? Mask(string? secret)
        => secret is null ? null : $"******** ({secret.Length} characters)";
}
