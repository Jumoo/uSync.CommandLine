using Umbraco.Cms.Core;

using uSync.Commands.Core;
using uSync.Commands.Core.Extensions;
using uSync.Expansions.Core;
using uSync.Expansions.Core.Restore;
using uSync.Expansions.Core.Restore.Services;
using uSync.Expansions.Core.Services;

namespace uSync.Complete.Commands;
internal class uSyncCreateRestorePointCommand : SyncCommandBase
{
    private readonly ISyncRestorePointService _syncRestorePointService;
    private readonly SyncPackService _packService;

    public uSyncCreateRestorePointCommand(
        ISyncRestorePointService syncRestorePointService,
        SyncPackService packService)
    {
        _syncRestorePointService = syncRestorePointService;
        _packService = packService;
    }

    public override string Id => "uSync-Restore-Create";

    public override string Name => "Create Restore Point";

    public override string Description => "Create a restore point";

    public override SyncCommandInfo GetCommand()
    {
        var command = base.GetCommand();

        command.Parameters = new List<SyncCommandParameterInfo>
        {
            new SyncCommandParameterInfo
            {
                Id= "IncludeMedia",
                Description = "Include Media files in the restore point",
                ParameterType = SyncCommandObjectType.Bool
            },
            new SyncCommandParameterInfo
            {
                Id = "Name",
                Description = "Name for the restore point",
                ParameterType = SyncCommandObjectType.String
            },
            new SyncCommandParameterInfo {
                Id = "Wait",
                Description = "Wait for the process to complete before returning",
                ParameterType = SyncCommandObjectType.Bool
            }
        };

        return command;
        
    }

    public override async Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        var includeMedia = request.GetParameterValue("IncludeMedia", false);
        var name = request.GetParameterValue("Name", $"Remote Restore point");
        var wait = request.GetParameterValue("Wait", true);

        var id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;

        if (!wait)
        {
            var attempt = QueueRestore(id, name, includeMedia);
            return new SyncCommandResponse(attempt.Result, attempt.Success)
            {
                Message = attempt.Success
                ? "Restore point creation has been queued"
                : $"Error {attempt.Exception?.Message ?? "An error occurred, check the logs"}",
                Complete = true,
            };
        }
        else
        {
            var syncPackRequest = new SyncPackRequest
            {
                RequestId = id,
                PageSize = 25,
                AdditionalData = request.AdditionalData,
            };

            // Sync pack request the restore 
            // (this loops)
            var attempt = await _packService.CreateRestorePoint(name, "uSyncCli", syncPackRequest);

            return new SyncCommandResponse(id, attempt.Success)
            {
                Complete = attempt.AllPagesProcessed,
                AdditionalData = attempt.AdditionalData               
            };


        }
    }

    private Attempt<Guid> QueueRestore(Guid id, string name, bool includeMedia)
    {
        return _syncRestorePointService.QueueCreate(
            id,
            name,
            "uSyncCli",
            includeMedia,
            "RemoteCommand");

    }
}
