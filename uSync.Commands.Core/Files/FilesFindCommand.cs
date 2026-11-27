using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.FolderChecks;

namespace uSync.Commands.Core.Files;

/// <summary>
///  which file(s) in the uSync folder hold an item, by key, alias or file name.
///  Handy when files-verify reports a clash. Offline.
/// </summary>
public class FilesFindCommand : SyncCommandBase, ISyncCommand
{
    private readonly Argument<string> _term = new("key-or-alias",
        "the item's key (a guid), alias, or file name");

    private readonly Option<string?> _folder = new("--folder",
        "uSync folder (default: from appsettings.json uSync:Settings:RootFolder, else the highest uSync/v{n} folder)");

    public Command Command { get; }

    public FilesFindCommand(TextWriter writer, IConfiguration configuration)
        : base(writer, configuration)
    {
        Command = new Command("files-find", "Find the uSync files for an item by key, alias or file name. Offline, no server needed.")
        {
            _term, _folder
        };

        Command.SetHandler(context =>
        {
            var result = context.ParseResult;
            context.ExitCode = FilesCommandHelper.Run(() =>
            {
                var term = result.GetValueForArgument(_term);
                var location = FilesCommandHelper.Locate(result.GetValueForOption(_folder));
                var loaded = new SyncFolderVerifier().Verify(location.Folder, new SyncFolderVerifyOptions { IncludeContent = true });

                var matches = Find(loaded.Items, term);
                if (matches.Count == 0)
                {
                    Writer.WriteLine($"Nothing in {location.Folder} matches '{term}'.");
                    return 1;
                }

                foreach (var item in matches)
                {
                    var marker = item.IsEmpty ? $"  ({item.Change ?? "Empty"} marker)" : "";
                    Writer.WriteLine($"{item.RelativePath}{marker}");
                    Writer.WriteLine($"  type: {item.TypeFolder}  alias: {item.Alias}  key: {item.Key}");
                }
                return 0;
            });
        });
    }

    public static IReadOnlyList<SyncFileItem> Find(IEnumerable<SyncFileItem> items, string term)
    {
        term = term.Trim();
        if (Guid.TryParse(term, out var key))
            return items.Where(x => x.Key == key).ToList();

        return items
            .Where(x => string.Equals(x.Alias, term, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileNameWithoutExtension(x.FilePath), term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
