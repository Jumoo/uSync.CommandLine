using Umbraco.Cms.Core.Hosting;

using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Controllers;
using uSync.BackOffice.SyncHandlers;

namespace uSync.Commands;
public class uSyncReportCommand : uSyncCommandBase
{
    public uSyncReportCommand(
        SyncHandlerFactory syncHandlerFactory,
        uSyncConfigService configService,
        uSyncService uSyncService,
        IHostingEnvironment hostingEnvironment) : base(syncHandlerFactory, configService, uSyncService, hostingEnvironment)
    { }

    public override string Id => "uSync-Report";

    public override string Name => "uSync Report";

    public override string Description => "Run a uSync report";

    protected override HandlerActions action => HandlerActions.Report;

    protected override async Task<SyncActionResult> RunHandlerCommand(string handler, string set, string folder, bool force)
    {
        var actions = await Task.FromResult(uSyncService.ReportHandler(handler, new uSyncImportOptions
        {
            HandlerSet = set,
            RootFolder = folder
        }));

        return new SyncActionResult(actions);
    }
}
