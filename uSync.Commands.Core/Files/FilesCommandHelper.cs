using uSync.Files;

namespace uSync.Commands.Core.Files;

/// <summary>
///  shared bits for the offline files-* commands.
/// </summary>
internal static class FilesCommandHelper
{
    public static SyncFolderLocator.SyncFolderLocation Locate(string? folder)
        => string.IsNullOrWhiteSpace(folder)
            ? SyncFolderLocator.Discover(Directory.GetCurrentDirectory())
            : SyncFolderLocator.FromFolder(Path.Combine(Directory.GetCurrentDirectory(), folder));

    /// <summary>
    ///  run a command, turning folder / git / file problems into one line and exit 2 (the tool failed).
    /// </summary>
    public static int Run(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is SyncFolderNotFoundException or GitException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }
}
