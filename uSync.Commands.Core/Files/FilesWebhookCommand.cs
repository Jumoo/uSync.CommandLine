using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.FolderChecks;

namespace uSync.Commands.Core.Files;

/// <summary>
///  writes the uSync webhook file for the "uSync Site Started" event, so a
///  deploy tells GitHub when the site is up. Offline, no server needed.
/// </summary>
public class FilesWebhookCommand : SyncCommandBase, ISyncCommand
{
    private readonly Option<string?> _repo = new("--repo",
        "GitHub repo (owner/name) whose workflow should run when the site starts");

    private readonly Option<string?> _url = new("--url",
        "send to this https url instead of GitHub");

    private readonly Option<string> _tokenName = new("--token-name", () => "GitHubToken",
        "name of the secret on the site (uSync:Notify:Secrets:{name}); empty for none");

    private readonly Option<string?> _name = new("--name",
        "name of the webhook in the backoffice");

    private readonly Option<string?> _folder = new("--folder",
        "uSync folder (default: from appsettings.json uSync:Settings:RootFolder, else the highest uSync/v{n} folder)");

    public Command Command { get; }

    public FilesWebhookCommand(TextWriter writer, IConfiguration configuration)
        : base(writer, configuration)
    {
        Command = new Command("files-webhook",
            "Add a 'uSync Site Started' webhook to the uSync folder, so the site tells your pipeline when a deploy is up. Needs the uSync.Command.Notify package on the site.")
        {
            _repo, _url, _tokenName, _name, _folder
        };

        Command.SetHandler(context =>
        {
            var result = context.ParseResult;
            try
            {
                var folderOption = result.GetValueForOption(_folder);
                var location = folderOption is null
                    ? SyncFolderLocator.Discover(Directory.GetCurrentDirectory())
                    : SyncFolderLocator.FromFolder(folderOption);

                var settings = new SiteStartedWebhookFile.Settings
                {
                    Repo = result.GetValueForOption(_repo),
                    Url = result.GetValueForOption(_url),
                    TokenName = result.GetValueForOption(_tokenName) ?? string.Empty,
                    Name = result.GetValueForOption(_name),
                };

                var file = SiteStartedWebhookFile.Create(settings);
                var path = SiteStartedWebhookFile.Write(location.Folder, file);

                Writer.WriteLine($"Wrote {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}");
                Writer.WriteLine($"  sends to: {file.Url}");
                Writer.WriteLine();
                Writer.WriteLine("Next:");
                Writer.WriteLine("  1. Add the uSync.Command.Notify package to the site.");
                if (string.IsNullOrWhiteSpace(settings.TokenName) is false)
                {
                    Writer.WriteLine($"  2. Set uSync__Notify__Secrets__{settings.TokenName} on the site to a token that can");
                    Writer.WriteLine("     create repository dispatch events (fine-grained, this repo only, Contents: read and write).");
                }
                Writer.WriteLine("  3. Commit the file. uSync imports it with the rest of your settings.");

                context.ExitCode = 0;
            }
            catch (Exception ex) when (ex is ArgumentException or SyncFolderNotFoundException)
            {
                Console.Error.WriteLine(ex.Message);
                context.ExitCode = 2;
            }
        });
    }
}
