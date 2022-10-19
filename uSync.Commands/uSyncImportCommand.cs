using Umbraco.Cms.Core.Hosting;

using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Controllers;
using uSync.BackOffice.SyncHandlers;
using uSync.Commands.Core;
using uSync.Core.Serialization;

namespace uSync.Commands;
public class uSyncImportCommand : uSyncCommandBase
{
    public uSyncImportCommand(
        SyncHandlerFactory syncHandlerFactory,
        uSyncConfigService configService,
        uSyncService uSyncService,
        IHostingEnvironment hostingEnvironment) : base(syncHandlerFactory, configService, uSyncService, hostingEnvironment)
    { }

    public override string Id => "uSync-Import";

    public override string Name => "uSync Import";

    public override string Description => "Run an uSync import";

    protected override HandlerActions action => HandlerActions.Import;

    public override SyncCommandInfo GetCommand()
    {
        var command = base.GetCommand();

        if (command.Parameters != null)
        {
            command.Parameters.Add(new SyncCommandParameterInfo
            {
                Id = "force",
                Description = "Force the import (regardless of changes)",
                ParameterType = SyncCommandObjectType.Bool,
            });
        }
        return command;
    }

    protected override async Task<SyncActionResult> RunHandlerCommand(string handler, string set, string folder, bool force)
    {
        var actions = await Task.FromResult(uSyncService.ImportHandler(handler, new BackOffice.uSyncImportOptions
        {
            HandlerSet = set,
            RootFolder = folder,
            Flags = force ? SerializerFlags.Force : SerializerFlags.None
        }));

        return new SyncActionResult(actions);
    }
}
