using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.User;

public class UserListCommand : ConnectedCommandBase, ISyncCommand
{
    public Command Command { get; }

    public UserListCommand(
        TextWriter wrtier,
        IConfiguration configuration,
        HttpClient client) : base(wrtier, configuration, client)
    {
        Command = new Command("user-list", "List all users");
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var users = await umbracoClient.GetUserAsync(0, 1000);

            await wrtier.WriteLineAsync($"Found {users.Total} users");

            foreach (var user in users.Items)
            {
                await wrtier.WriteLineAsync($"Umbraco User: {user.Name} {user.Email} {user.Id}");
            }
            context.ExitCode = 0;
        });
    }
}
