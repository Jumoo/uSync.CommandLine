namespace uSync.Commands.Core;

/// <summary>
///  Information about a command
/// </summary>
public class SyncCommandInfo
{
    /// <summary>
    ///  the Id for the command (what its called)
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    ///  description of what the command does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///  list of parameters that can be sent over to the command 
    /// </summary>
    public IList<SyncCommandParameterInfo>? Parameters { get; set; }
}

/// <summary>
///  information about a parameter of a command 
/// </summary>
public class SyncCommandParameterInfo
{
    /// <summary>
    ///  Id for the parameter (what its called)
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    ///  a short description of what the parameter is for
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///  the type of the parameter value
    /// </summary>
    /// <remarks>
    ///  the type is not enforced by the command line, but is to act as a guide
    ///  it is the responsibly of the command class to check/enforce its parameters
    /// </remarks>
    public SyncCommandObjectType ParameterType { get; set; }
}

public enum SyncCommandObjectType
{
    String,
    Int,
    Bool,
    Json
}