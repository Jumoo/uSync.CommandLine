using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using uSync.Commands.Core.Commands;

namespace uSync.FolderChecks.Tests;

/// <summary>
///  connection settings: command line, then environment variables, then appsettings.
/// </summary>
public class ConnectionParameterTests
{
    private class ConnectionCommand : SyncCommandBase
    {
        public Command Command { get; } = new("connect");

        public ConnectionCommand(IConfiguration configuration) : base(TextWriter.Null, configuration)
        {
            AddCoreOptions(Command);
        }
    }

    private static SyncConnectionParameters Get(string[] args, Dictionary<string, string?> environment, Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        var command = new ConnectionCommand(configuration)
        {
            GetEnvironmentVariable = name => environment.TryGetValue(name, out var value) ? value : null
        };

        var parseResult = new Parser(command.Command).Parse(args);
        return command.GetConnectionPartameters(new InvocationContext(parseResult));
    }

    private static readonly Dictionary<string, string?> Environment = new()
    {
        ["USYNC_SERVER"] = "https://env.example.com",
        ["USYNC_CLIENT_ID"] = "umbraco-back-office-env",
        ["USYNC_CLIENT_SECRET"] = "env-secret",
    };

    [Test]
    public void Environment_Variables_Are_Used()
    {
        var connection = Get([], Environment);

        Assert.That(connection.Url.ToString(), Is.EqualTo("https://env.example.com/"));
        Assert.That(connection.ClientId, Is.EqualTo("umbraco-back-office-env"));
        Assert.That(connection.ClientSecret, Is.EqualTo("env-secret"));
    }

    [Test]
    public void Server_From_Environment_Gets_A_Trailing_Slash()
    {
        var environment = new Dictionary<string, string?>(Environment) { ["USYNC_SERVER"] = "https://env.example.com/site" };
        Assert.That(Get([], environment).Url.ToString(), Is.EqualTo("https://env.example.com/site/"));
    }

    [Test]
    public void Command_Line_Beats_Environment()
    {
        var connection = Get(["-s", "https://cli.example.com/", "-i", "umbraco-back-office-cli", "-k", "cli-secret"], Environment);

        Assert.That(connection.Url.ToString(), Is.EqualTo("https://cli.example.com/"));
        Assert.That(connection.ClientId, Is.EqualTo("umbraco-back-office-cli"));
        Assert.That(connection.ClientSecret, Is.EqualTo("cli-secret"));
    }

    [Test]
    public void Environment_Beats_AppSettings()
    {
        var connection = Get([], Environment, new()
        {
            ["uSync:Command:ServerUrl"] = "https://config.example.com/",
            ["uSync:Command:ClientId"] = "umbraco-back-office-config",
            ["uSync:Command:Secret"] = "config-secret",
        });

        Assert.That(connection.Url.Host, Is.EqualTo("env.example.com"));
        Assert.That(connection.ClientId, Is.EqualTo("umbraco-back-office-env"));
        Assert.That(connection.ClientSecret, Is.EqualTo("env-secret"));
    }

    [Test]
    public void Missing_Secret_Says_Where_To_Set_It_Without_Echoing_Values()
    {
        var environment = new Dictionary<string, string?>(Environment) { ["USYNC_CLIENT_SECRET"] = null };
        var ex = Assert.Throws<SyncUsageException>(() => Get([], environment));
        Assert.That(ex!.Message, Does.Contain("USYNC_CLIENT_SECRET"));
    }
}
