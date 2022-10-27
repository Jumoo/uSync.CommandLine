//
// the uSync command line.
//

using System.CommandLine;
using Microsoft.Extensions.Logging;
using uSync.Handlers;

var loggerFactory = LoggerFactory.Create(b =>
{
    b.AddConsole();
    b.SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger("uSyncCommand");

var commands = new List<ISyncCommandHandler>
{
    new RunCommandHandler(logger, Console.Out),
    new ListCommandHandler(logger, Console.Out),
    new PingCommandHandler(logger, Console.Out),
    new KeyGenCommandHandler(Console.Out),
    new ServerCommandHandler()
};

var rootCommand = new RootCommand("uSync command line");

Console.Out.WriteLine("  *** uSync Command Line ***");
Console.Out.WriteLine("");

foreach (var command in commands)
{
    if (command.Command is null) continue;
    rootCommand.AddCommand(command.Command);
}

var result = await rootCommand.InvokeAsync(args);

// if there is an error we sleep for 1/2 a second, just lets logging catch up
if (result != 0) Thread.Sleep(500);


