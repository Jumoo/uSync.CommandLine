using uSync.Commands.Core;

namespace uSync.Commands.Server.Services;

internal class SyncCommandService : ISyncCommandService
{
    private readonly SyncCommandCollection _commands;

    public SyncCommandService(SyncCommandCollection commands)
    {
        _commands = commands;
    }

    public ISyncCommand? FindCommand(string id)
        => _commands.FindCommand(id);

    public IReadOnlyCollection<SyncCommandInfo> ListCommands()
        => _commands.Select(x => x.GetCommand()).ToList();

    public async Task<SyncCommandResponse> Execute(string id, SyncCommandRequest request)
    {
        var command = FindCommand(id);

        if (command == null)
            throw new EntryPointNotFoundException($"No command with id : {id} found");

        if (request.Id == Guid.Empty)
            request.Id = Guid.NewGuid();

        return await command.Execute(request);
    }
}
