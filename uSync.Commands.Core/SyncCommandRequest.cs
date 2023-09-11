namespace uSync.Commands.Core;

/// <summary>
///  A request object for any command execution
/// </summary>
public class SyncCommandRequest
{
    /// <summary>
    ///  the unique id for this request chain.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///  the Id of the command. 
    /// </summary>
    public string? CommandId { get; set; }

    /// <summary>
    ///  parameter values for the request
    /// </summary>
    public IReadOnlyCollection<SyncCommandParameter> Parameters { get; init; }
        = new List<SyncCommandParameter>();

    /// <summary>
    ///  the action Id (if the command has actions)
    /// </summary>
    public string? ActionId { get; set; }

    /// <summary>
    ///  the step index, e.g what step we are up to.
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    ///  the current page of a request 
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    ///  the total amount of items per page 
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    ///  additional data that is passed around between requests
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
///  a parameter value for a command
/// </summary>
public class SyncCommandParameter
{
    public string? Id { get; set; }
    public object? Value { get; set; }
}