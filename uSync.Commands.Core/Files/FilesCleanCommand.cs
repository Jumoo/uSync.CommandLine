using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Files;

namespace uSync.Commands.Core.Files;

public sealed record DeleteMarker(string RelativePath, string FilePath, string Type, string Alias, DateTimeOffset? Committed);

/// <summary>
///  list uSync's delete markers (&lt;Empty Change="Delete"&gt; files) with their age,
///  and remove the old ones. Offline, uses git for the age.
/// </summary>
/// <remarks>
///  a delete marker is how uSync tells other sites to delete an item, so it can only
///  go once every site has imported it - hence --older-than.
/// </remarks>
public class FilesCleanCommand : SyncCommandBase, ISyncCommand
{
    private readonly Option<int?> _olderThan = new("--older-than",
        "only markers last committed more than this many days ago");

    private readonly Option<bool> _remove = new("--remove",
        "delete the marker files (needs --older-than). Commit the result like any other change.");

    private readonly Option<string?> _folder = new("--folder",
        "uSync folder (default: from appsettings.json uSync:Settings:RootFolder, else the highest uSync/v{n} folder)");

    public Command Command { get; }

    public FilesCleanCommand(TextWriter writer, IConfiguration configuration)
        : base(writer, configuration)
    {
        Command = new Command("files-clean", "List uSync's delete markers with their age, and remove old ones with --remove. Offline.")
        {
            _olderThan, _remove, _folder
        };

        Command.SetHandler(context =>
        {
            var result = context.ParseResult;
            var olderThan = result.GetValueForOption(_olderThan);
            var remove = result.GetValueForOption(_remove);

            if (remove && olderThan is null)
                throw new SyncUsageException("--remove needs --older-than <days>: a delete marker has to stay until every site has imported it.");
            if (olderThan < 0)
                throw new SyncUsageException("--older-than can't be negative.");

            context.ExitCode = FilesCommandHelper.Run(() =>
            {
                var location = FilesCommandHelper.Locate(result.GetValueForOption(_folder));
                var loaded = new SyncFolderVerifier().Verify(location.Folder, new SyncFolderVerifyOptions { IncludeContent = true });

                var markers = loaded.Items
                    .Where(x => x.IsDelete)
                    .Select(x => new DeleteMarker(x.RelativePath, x.FilePath, x.TypeFolder, x.Alias, GitChanges.GetLastCommitDate(x.FilePath)))
                    .OrderBy(x => x.Committed ?? DateTimeOffset.MaxValue)
                    .ToList();

                var now = DateTimeOffset.Now;
                var selected = Select(markers, olderThan, now);

                Writer.WriteLine($"{markers.Count} delete marker(s) in {location.Folder}" +
                    (olderThan is null ? "" : $", {selected.Count} older than {olderThan} days"));

                foreach (var marker in markers)
                {
                    var age = marker.Committed is null ? "not committed" : $"{(int)(now - marker.Committed.Value).TotalDays} days";
                    var flag = olderThan is not null && selected.Contains(marker) ? (remove ? "removed " : "old     ") : "        ";
                    Writer.WriteLine($"  {flag}{age,14}  {marker.RelativePath}  ({marker.Alias})");
                }

                if (remove)
                {
                    foreach (var marker in selected)
                        File.Delete(marker.FilePath);

                    if (selected.Count > 0)
                        Writer.WriteLine($"Removed {selected.Count} file(s). Commit the change to apply it.");
                }

                return 0;
            });
        });
    }

    /// <summary>
    ///  markers committed more than <paramref name="olderThanDays"/> days ago.
    ///  Uncommitted markers are never old.
    /// </summary>
    public static IReadOnlyList<DeleteMarker> Select(IEnumerable<DeleteMarker> markers, int? olderThanDays, DateTimeOffset now)
        => olderThanDays is null
            ? []
            : markers.Where(x => x.Committed is not null && now - x.Committed.Value > TimeSpan.FromDays(olderThanDays.Value)).ToList();
}
