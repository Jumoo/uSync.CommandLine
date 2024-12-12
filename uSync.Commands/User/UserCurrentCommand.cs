using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.User;

/// <summary>
///  fetch the current user (simple proves we can talk to umbraco).
/// </summary>
public class UserCurrentCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public UserCurrentCommand(TextWriter writer,
        IConfiguration configuration,
        HttpClient client)
        : base(writer, configuration, client)
    {
        Command = new Command("user-current", "Fetch the user executing the commands");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);

            var user = await umbracoClient.GetUserCurrentAsync();
            await writer.WriteLineAsync($"Umbraco User: {user.Name} {user.Email} {user.Id}");

            context.ExitCode = 0;
        });
    }
}
