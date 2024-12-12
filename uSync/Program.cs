using System.CommandLine.Parsing;
using System.Diagnostics;

using uSync;
using uSync.Commands.Cache;
using uSync.Commands.Core.Commands;
using uSync.Commands.HealthChecks;
using uSync.Commands.Index;
using uSync.Commands.Models;
using uSync.Commands.User;
using uSync.Commands.uSync;

var serviceProvider = BuildHelper.BuildServiceProvider(
    [
        typeof(TestCommand),
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
        typeof(uSyncPingCommand)
    ]);

var parser = BuildHelper.BuildParser(serviceProvider);

var fileVersionInfo = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);

var version = fileVersionInfo.ProductVersion ?? "15.0.0";
if (version.IndexOf('+') > 0)
{
     version = version.Substring(0, version.IndexOf('+'));
}

Console.WriteLine($"uSync CommandLine: {version ?? "15.0.0"}");
Console.WriteLine("");

return await parser.InvokeAsync(args);
