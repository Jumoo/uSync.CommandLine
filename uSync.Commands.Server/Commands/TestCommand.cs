using uSync.Commands.Core;
using uSync.Commands.Core.Extensions;

namespace uSync.Commands.Server.Commands;
internal class TestCommand : SyncCommandBase
{
    public override string Id => "Test";

    public override string Name => "Test command";

    public override string Description => "A test command, to check things work";

    public override SyncCommandInfo GetCommand()
    {
        var info = base.GetCommand();
        info.Parameters = new List<SyncCommandParameterInfo>
        {
            new SyncCommandParameterInfo
            {
                Id = "Count",
                ParameterType = SyncCommandObjectType.Int
            }
        };

        return info;
    }


    public override Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        var pageCount = request.GetParameterValue("count", 10);

        return Task.FromResult(new SyncCommandResponse(request.Id, true)
        {
            Complete = request.Page >= pageCount - 1,
            Result = $"Hello from the server {request.Page + 1}",
            TotalPages = pageCount
        });
    }
}
