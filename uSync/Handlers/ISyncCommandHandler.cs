using System.CommandLine;

namespace uSync.Handlers;

/// <summary>
///  A command handler for the uSync command line. 
/// </summary>
/// <remarks>
///  Command Handlers mange the console side 
///  of the command line (they do not run on the server)
///  
///  you probably don't need to add anything here
///  if you want to run something on an Umbraco site
///  you need to implement ISyncCommand and have
///  the code run on the Umbraco site. 
/// </remarks>
internal interface ISyncCommandHandler
{
    /// <summary>
    ///  The command
    /// </summary>
    public Command? Command { get; }
}
