using Microsoft.Extensions.Configuration;

using System.CommandLine;

namespace uSync.Commands.Core.Commands;

/// <summary>
///  test command, just proves that we are loading and running commands. 
/// </summary>
public class TestCommand : SyncCommandBase, ISyncCommand
{
    public Command Command { get; }

    public TestCommand(TextWriter writer, IConfiguration configuration)
        : base(writer, configuration)
    {
        Command = new Command("test", "Test command");
        Command.SetHandler(async (context) =>
        {
            await writer.WriteLineAsync("Hello from test command");
        });
    }
}
