using System.CommandLine.Parsing;
using System.Diagnostics;

using uSync;
using uSync.Commands.Cache;
using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Files;
using uSync.Commands.HealthChecks;
using uSync.Commands.Index;
using uSync.Commands.Models;
using uSync.Commands.User;
using uSync.Commands.uSync;

var serviceProvider = BuildHelper.BuildServiceProvider(
    [
        typeof(TestCommand),
        typeof(FilesVerifyCommand),
        typeof(FilesWebhookCommand),
        typeof(FilesDiffCommand),
        typeof(FilesFindCommand),
        typeof(FilesCleanCommand),
        typeof(UserCurrentCommand),
        typeof(UserListCommand),
        typeof(CacheRebuildCommand),
        typeof(CacheReloadCommand),
        typeof(ModelsRebuildCommand),
        typeof(ModelsStatusCommand),
        typeof(IndexerRebuildCommand),
        typeof(IndexerListCommand),
        typeof(HealthCheckListCommand),
        typeof(HealthCheckGroupListCommand),
        typeof(HealthCheckGroupCheckCommand),
        typeof(uSyncSettingsCommand),
        typeof(uSyncImportCommand),
        typeof(uSyncExportCommand),
        typeof(uSyncPingCommand),
        typeof(uSyncReportCommand)
    ]);

var parser = BuildHelper.BuildParser(serviceProvider);

var fileVersionInfo = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);

var version = fileVersionInfo.ProductVersion ?? "15.0.0";
if (version.IndexOf('+') > 0)
{
     version = version.Substring(0, version.IndexOf('+'));
}

// the banner goes to stderr, so stdout can carry sarif / junit output.
Console.Error.WriteLine($"uSync CommandLine: {version ?? "15.0.0"}");
Console.Error.WriteLine("");

return await parser.InvokeAsync(args);
