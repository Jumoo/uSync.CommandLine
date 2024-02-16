using Umbraco.Cms.Core.Hosting;

using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Models;
using uSync.BackOffice.SyncHandlers;

namespace uSync.Commands;
public class uSyncExportCommand : uSyncCommandBase
{
    public uSyncExportCommand(
        SyncHandlerFactory syncHandlerFactory,
        uSyncConfigService configService,
        uSyncService uSyncService,
        IHostingEnvironment hostingEnvironment) : base(syncHandlerFactory, configService, uSyncService, hostingEnvironment)
    { }

    public override string Id => "uSync-Export";

    public override string Name => "uSync Export";

    public override string Description => "Run an uSync export";

    protected override HandlerActions action => HandlerActions.Export;

    protected override async Task<SyncActionResult> RunHandlerCommand(string handler, string set, string folder, bool force)
    {
        var actions = await Task.FromResult(uSyncService.ExportHandler(handler, new uSyncImportOptions
        {
            HandlerSet = set,
            RootFolder = folder
        }));

        return new SyncActionResult(actions);
    }
}
