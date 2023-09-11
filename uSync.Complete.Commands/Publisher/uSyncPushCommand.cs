using uSync.BackOffice.SyncHandlers;
using uSync.Commands.Core;
using uSync.Commands.Core.Extensions;
using uSync.Core.Dependency;
using uSync.Publisher.Client;
using uSync.Publisher.Models;

namespace uSync.Complete.Commands.Publisher;
internal class uSyncPushCommand : uSyncCompleteCommandBase
{
    public uSyncPushCommand(
        SyncHandlerFactory syncHandlerFactory,
        IPublisherStateService publisherStateService)
        : base(syncHandlerFactory, publisherStateService)
    { }

    public override string Id => "uSync-Push";
    public override string Name => "uSync Push";
    public override string Description => "Push updates via uSync.Publisher";

    public override SyncCommandInfo GetCommand()
    {
        var command = base.GetCommand();

        command.Parameters = new List<SyncCommandParameterInfo>
        {
            new SyncCommandParameterInfo
            {
                 Id = "server",
                 Description = "Server to push to",
                 ParameterType = SyncCommandObjectType.String
            },
            new SyncCommandParameterInfo
            {
                Id = "group",
                Description = "Handler group to use (e.g settings, content)",
                ParameterType = SyncCommandObjectType.String
            }
        };

        return command;

    }

    public override async Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        var group = request.GetParameterValue("group", "settings");

        var items = GetSyncItems(group, DependencyFlags.IncludeChildren);
        return await PerformCommand(request, items, GetModeFromGroup(group));
    }

    public static PublishMode GetModeFromGroup(string group)
    {
        return group switch
        {
            "content" => PublishMode.Push,
            _ => PublishMode.SettingsPush,
        };
    }
}
