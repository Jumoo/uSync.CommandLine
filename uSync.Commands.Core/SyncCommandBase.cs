namespace uSync.Commands.Core;

/// <summary>
///  Base class to remove some boilerplate code when implementing ISyncCommand
/// </summary>
public abstract class SyncCommandBase : ISyncCommand
{
    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract Task<SyncCommandResponse> Execute(SyncCommandRequest request);

    /// <inheritdoc/>
    public virtual SyncCommandInfo GetCommand()
        => new SyncCommandInfo
        {
            Id = Id,
            Description = Description
        };
}
