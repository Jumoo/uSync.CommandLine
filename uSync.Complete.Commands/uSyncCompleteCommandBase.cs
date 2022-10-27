using Umbraco.Cms.Core;

using uSync.BackOffice.SyncHandlers;
using uSync.Commands.Core;
using uSync.Commands.Core.Extensions;
using uSync.Core.Dependency;
using uSync.Core.Sync;
using uSync.Publisher;
using uSync.Publisher.Client;
using uSync.Publisher.Models;

namespace uSync.Complete.Commands;
public abstract class uSyncCompleteCommandBase : SyncCommandBase
{
    protected readonly SyncHandlerFactory syncHandlerFactory;
    protected readonly IPublisherStateService publisherStateService;

    protected uSyncCompleteCommandBase(
        SyncHandlerFactory syncHandlerFactory,
        IPublisherStateService publisherStateService)
    {
        this.syncHandlerFactory = syncHandlerFactory;
        this.publisherStateService = publisherStateService;
    }

    protected async Task<SyncCommandResponse> PerformCommand(SyncCommandRequest request,
        IEnumerable<SyncItem> items, PublishMode mode)
    {
        var remoteServer = request.GetParameterValue("server", string.Empty);

        if (!publisherStateService.HasProcess(request.Id))
        {
            publisherStateService.Intiailize(request.Id, remoteServer, mode, items);
        }
        
        var result = await publisherStateService.Process(request.Id);

        return new SyncCommandResponse
        {
            Complete = result.IsComplete,
            Success = result.Sucess,
            TotalPages = 1000,
            Message = $"{result.Sucess} {result.Message}",
            Result = $"## {result.Sucess} {result.Message} ##",
            ResultType = SyncCommandObjectType.String
        };

    }


    protected virtual IEnumerable<SyncItem> GetSyncItems(string groups, DependencyFlags flags)
    {
        var handlers = syncHandlerFactory.GetValidHandlers(new SyncHandlerOptions
        {
            Set = uSyncPublisher.PublisherHandlerSet,
            Group = groups
        });

        return handlers.Select(x => new SyncItem(flags)
        {
            Name = x.Handler.TypeName,
            Udi = Udi.Create(x.Handler.EntityType)
        });
    }
}
