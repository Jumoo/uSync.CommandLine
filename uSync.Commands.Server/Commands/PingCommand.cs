using uSync.Commands.Core;

namespace uSync.Commands.Server.Commands;
internal class PingCommand : SyncCommandBase
{
    public override string Id => "Ping";

    public override string Name => "Ping command";

    public override string Description => "Ping a server, returns true when server is ready";

    public override Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        return Task.FromResult(new SyncCommandResponse(request.Id, true)
        {
            ResultType = SyncCommandObjectType.Bool,
            Result = true
        });
    }
}
