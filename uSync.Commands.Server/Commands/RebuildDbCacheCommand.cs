using Umbraco.Cms.Core.PublishedCache;

using uSync.Commands.Core;

namespace uSync.Commands.Server.Commands;
internal class RebuildDbCacheCommand : SyncCommandBase
{
    private readonly IPublishedSnapshotService _publishedSnapshotService;
    private readonly IPublishedSnapshotStatus _publishedSnapshotStatus;

    public RebuildDbCacheCommand(IPublishedSnapshotService publishedSnapshotService, IPublishedSnapshotStatus publishedSnapshotStatus)
    {
        _publishedSnapshotService = publishedSnapshotService;
        _publishedSnapshotStatus = publishedSnapshotStatus;
    }

    public override string Id => "Rebuild-DbCache";

    public override string Name => "Rebuild database cache";

    public override string Description => "Rebuilds the database cache (Expensive)";

    public override Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        _publishedSnapshotService.Rebuild();
        var status = _publishedSnapshotStatus.GetStatus();

        return Task.FromResult(new SyncCommandResponse(request.Id, true)
        {
            ResultType = SyncCommandObjectType.String,
            Result = status
        });
    }
}
