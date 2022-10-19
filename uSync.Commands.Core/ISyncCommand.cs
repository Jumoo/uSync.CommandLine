namespace uSync.Commands.Core;

/// <summary>
///  Interface for a command
/// </summary>
/// <remarks>
///  any public class implementing this interface will be added to the list of 
///  commands that a server will respond with when the 'list' command is sent
/// </remarks>
public interface ISyncCommand
{
    /// <summary>
    ///  Id for the command
    /// </summary>
    string Id { get; }

    /// <summary>
    ///  display name for the command 
    /// </summary>
    string Name { get; }

    /// <summary>
    ///  a short description of the command
    /// </summary>
    string Description { get; }

    /// <summary>
    ///  return the detailed information about the command
    /// </summary>
    SyncCommandInfo GetCommand();


    /// <summary>
    ///  execute the command 
    /// </summary>
    /// <remarks>
    ///  if the command has steps or is paged, then the execute 
    ///  command will need to handle that.
    /// </remarks>
    Task<SyncCommandResponse> Execute(SyncCommandRequest request);
}