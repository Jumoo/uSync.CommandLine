using uSync.Commands.Core;

namespace uSync.Commands.Server.Services;
public interface ISyncCommandService
{
    ISyncCommand? FindCommand(string id);
    Task<SyncCommandResponse> Execute(string id, SyncCommandRequest request);
    IReadOnlyCollection<SyncCommandInfo> ListCommands();
}