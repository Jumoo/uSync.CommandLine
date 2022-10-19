using System.CommandLine;
using System.Security.Cryptography;

namespace uSync.Handlers;
internal class ServerCommandHandler : ISyncCommandHandler
{
    public Command? Command { get; private set; }
  
}
