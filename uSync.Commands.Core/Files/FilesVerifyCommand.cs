using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Core.Files;

/// <summary>
///  checks the uSync folder for problems - offline, no server or keys needed.
/// </summary>
public class FilesVerifyCommand : SyncCommandBase, ISyncCommand
{
    private readonly Option<string?> _folder = new("--folder",
        "uSync folder (default: from appsettings.json uSync:Settings:RootFolder, else the highest uSync/v{n} folder)");

    private readonly Option<bool> _content = new("--content",
        "also check the Content, Media and Blueprints folders");

    private readonly Option<string?> _format = new Option<string?>("--format",
        "output format (default: github when GITHUB_ACTIONS=true, else text)")
        .FromAmong(FilesVerifyFormats.All);

    private readonly Option<string?> _output = new("--output",
        "write the output (sarif / junit) to this file instead of the console");

    private readonly Option<bool> _warningsAsErrors = new("--warnings-as-errors",
        "fail (exit 1) on warnings too");

    private readonly Option<string?> _changedSince = new("--changed-since",
        "only report problems in files changed since this git ref (e.g. origin/main)");

    private readonly Option<bool> _summary = new("--summary",
        "write a markdown summary to $GITHUB_STEP_SUMMARY (or the console), listing the items changed since --changed-since");

    private readonly Option<string?> _publishFolder = new("--publish-folder",
        "check this publish output contains the uSync folder and its files");

    public Command Command { get; }

    public FilesVerifyCommand(TextWriter writer, IConfiguration configuration)
        : base(writer, configuration)
    {
        Command = new Command("files-verify", "Check the uSync folder for problems (merge conflicts, clashes, broken files). Offline, no server needed.")
        {
            _folder, _content, _format, _output, _warningsAsErrors, _changedSince, _summary, _publishFolder
        };

        Command.SetHandler(context =>
        {
            var result = context.ParseResult;
            var settings = new FilesVerifySettings
            {
                Folder = result.GetValueForOption(_folder),
                Content = result.GetValueForOption(_content),
                Format = result.GetValueForOption(_format),
                Output = result.GetValueForOption(_output),
                WarningsAsErrors = result.GetValueForOption(_warningsAsErrors),
                ChangedSince = result.GetValueForOption(_changedSince),
                Summary = result.GetValueForOption(_summary),
                PublishFolder = result.GetValueForOption(_publishFolder),
            };

            context.ExitCode = new FilesVerifyRunner(Writer, Console.Error).Run(settings);
        });
    }
}
