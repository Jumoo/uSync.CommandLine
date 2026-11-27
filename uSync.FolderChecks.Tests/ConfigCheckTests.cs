using Microsoft.Extensions.Configuration;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using uSync.Commands.Config;
using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.FolderChecks.Tests;

public class ConfigCheckTests
{
    private class ConnectionCommand : SyncCommandBase
    {
        public Command Command { get; } = new("connect");

        public ConnectionCommand(IConfiguration configuration) : base(TextWriter.Null, configuration)
            => AddCoreOptions(Command);
    }

    private static SyncConnectionDescription Describe(string[] args, Dictionary<string, string?> environment, Dictionary<string, string?>? settings = null)
    {
        var command = new ConnectionCommand(new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build())
        {
            GetEnvironmentVariable = name => environment.TryGetValue(name, out var value) ? value : null
        };
        return command.DescribeConnection(new InvocationContext(new Parser(command.Command).Parse(args)));
    }

    [Test]
    public void Each_Setting_Says_Where_It_Came_From()
    {
        var connection = Describe(
            ["--secret", "from-args"],
            new() { ["USYNC_SERVER"] = "https://env.example.com" },
            new() { ["uSync:Command:ClientId"] = "umbraco-back-office-settings" });

        Assert.That(connection.Server, Is.EqualTo(new SyncConnectionSetting("https://env.example.com/", "USYNC_SERVER")));
        Assert.That(connection.ClientId, Is.EqualTo(new SyncConnectionSetting("umbraco-back-office-settings", "appsettings.json uSync:Command:ClientId")));
        Assert.That(connection.Secret, Is.EqualTo(new SyncConnectionSetting("from-args", "--secret")));
    }

    [Test]
    public void Missing_Settings_Are_Null_Not_Errors()
    {
        var connection = Describe([], []);
        Assert.That(connection.ClientId.Value, Is.Null);
        Assert.That(connection.Secret.Source, Is.Null);
    }

    [Test]
    public void The_Secret_Is_Never_Shown()
    {
        Assert.That(ConfigCheckCommand.Mask("0715636f-c11a-4427"), Is.EqualTo("******** (18 characters)"));
        Assert.That(ConfigCheckCommand.Mask(null), Is.Null);
    }

    private enum Colour { Red }

    [Test]
    public void Json_Uses_Camel_Case_And_Enum_Names()
        => Assert.That(JsonOutput.Serialize(new { Name = "x", Colour = Colour.Red, Missing = (string?)null }).Replace("\r", ""),
            Is.EqualTo("{\n  \"name\": \"x\",\n  \"colour\": \"Red\"\n}"));
}
