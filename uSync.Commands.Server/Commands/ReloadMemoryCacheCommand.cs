using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;

using uSync.Commands.Core;

namespace uSync.Commands.Server.Commands;
internal class ReloadMemoryCacheCommand : SyncCommandBase
{
    private readonly DistributedCache _distributedCache;

    public ReloadMemoryCacheCommand(DistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public override string Id => "Reload-MemCache";
    public override string Name => "Reload the memory cache";
    public override string Description => "Reloads the in-memory cache";

    public override Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        _distributedCache.RefreshAllPublishedSnapshot();

        return Task.FromResult(new SyncCommandResponse(request.Id, true)
        {
            ResultType = SyncCommandObjectType.String,
            Result = "MemoryCache refreshed"
        });
    }
}
