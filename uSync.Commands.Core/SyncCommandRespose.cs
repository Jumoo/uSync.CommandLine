namespace uSync.Commands.Core;

/// <summary>
///  response object returned by a command 
/// </summary>
public class SyncCommandResponse
{
    // by default all commands are single step (so complete in one)
    // if you have a multi-step command, you have to set complete
    // on each return.
    public SyncCommandResponse()
        => Complete = true;

    public SyncCommandResponse(Guid id)
        : this()
    {
        Id = id;
    }

    public SyncCommandResponse(Guid id, bool success)
        : this(id)
    {
        Success = success;
    }

    /// <summary>
    ///  id value of the command/response
    /// </summary>
    /// <remarks>
    ///  if there are multiple commands you will want to group them
    ///  so you know they are all part of the same 'process'.
    ///  
    ///  the request/response Id means you can tell when all the request
    ///  are from the same one.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    ///  the command executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///  the process is complete (there are no more steps or pages)
    /// </summary>
    public bool Complete { get; set; }

    /// <summary>
    ///  a single step has finished
    /// </summary>
    public bool StepComplete { get; set; }

    /// <summary>
    ///  we have incremented the step index for this process
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    ///  the alias of the action to call if you are going to recall this command
    /// </summary>
    public string NextAction { get; set; } = string.Empty;

    /// <summary>
    ///  total number of pages we expect in this step
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    ///  a message to display to the user during the process
    /// </summary>
    public string? Message { get; set; } = string.Empty;

    /// <summary>
    ///  the result of the command
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    ///  the object type for the result. 
    /// </summary>
    public SyncCommandObjectType ResultType { get; set; }

    public bool ResetPaging { get; set; }

    /// <summary>
    ///  any additional data that might get passed around while chaining request. 
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}