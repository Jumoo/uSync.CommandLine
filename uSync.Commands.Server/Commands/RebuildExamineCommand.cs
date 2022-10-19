using Umbraco.Cms.Infrastructure.Examine;

using uSync.Commands.Core;
using uSync.Commands.Core.Extensions;

namespace uSync.Commands.Server.Commands;
internal class RebuildExamineCommand : SyncCommandBase
{
    private const string c_defaultIndex = "ExternalIndex";
    public override string Id => "Rebuild-Index";
    public override string Name => "Rebuild examine Index";
    public override string Description => "Rebuilds an examine index";

    private readonly IIndexRebuilder _rebuilder;
    public RebuildExamineCommand(IIndexRebuilder rebuilder)
    {
        _rebuilder = rebuilder;
    }

    public override SyncCommandInfo GetCommand()
    {
        var command = base.GetCommand();

        command.Parameters = new List<SyncCommandParameterInfo>
        {
            new SyncCommandParameterInfo
            {
                Id = "index",
                ParameterType = SyncCommandObjectType.String,
                Description = $"Examine index to rebuild (default: '{c_defaultIndex}'"
            }
        };
        return command;
    }

    public override Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        var index = c_defaultIndex;

        index = request.GetParameterValue("index", index);

        var response = new SyncCommandResponse(request.Id);

        try
        {
            if (_rebuilder.CanRebuild(index))
            {
                _rebuilder.RebuildIndex(index);
                response.Result = $"Index build started for [{index}]";
            }
            else
            {
                response.Success = false;
                response.Message = $"Cannot rebuild {index}";
            }
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Error: {ex.Message}";
        }

        return Task.FromResult(response);
    }
}
