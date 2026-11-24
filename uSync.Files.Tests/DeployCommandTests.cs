using Umbraco.Management.Api;

using uSync.Commands.HealthChecks;
using uSync.Commands.LogViewer;
using uSync.Commands.Server;

namespace uSync.Files.Tests;

public class DeployCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [TestCase("90s", 0, 1, 30)]
    [TestCase("15m", 0, 15, 0)]
    [TestCase("2h", 2, 0, 0)]
    [TestCase(" 2H ", 2, 0, 0)]
    public void Since_Reads_Durations(string value, int hours, int minutes, int seconds)
    {
        Assert.That(LogSince.TryParse(value, Now, out var since), Is.True);
        Assert.That(Now - since, Is.EqualTo(new TimeSpan(hours, minutes, seconds)));
    }

    [Test]
    public void Since_Reads_Days()
    {
        Assert.That(LogSince.TryParse("1d", Now, out var since), Is.True);
        Assert.That(Now - since, Is.EqualTo(TimeSpan.FromDays(1)));
    }

    [Test]
    public void Since_Reads_A_Date()
    {
        Assert.That(LogSince.TryParse("2026-11-20T08:30:00+00:00", Now, out var since), Is.True);
        Assert.That(since, Is.EqualTo(new DateTimeOffset(2026, 11, 20, 8, 30, 0, TimeSpan.Zero)));
    }

    [TestCase("")]
    [TestCase("banana")]
    [TestCase("15x")]
    [TestCase("-5m")]
    public void Since_Rejects_Nonsense(string value)
        => Assert.That(LogSince.TryParse(value, Now, out _), Is.False);

    [Test]
    public void Levels_Include_The_Worse_Ones()
    {
        Assert.That(LogErrorsCommand.LevelsFrom(LogLevelModel.Warning),
            Is.EqualTo(new[] { LogLevelModel.Warning, LogLevelModel.Error, LogLevelModel.Fatal }));
        Assert.That(LogErrorsCommand.LevelsFrom(LogLevelModel.Fatal), Is.EqualTo(new[] { LogLevelModel.Fatal }));
    }

    [TestCase("error", 0, 3, false)]
    [TestCase("error", 1, 0, true)]
    [TestCase("warning", 0, 1, true)]
    [TestCase("warning", 0, 0, false)]
    [TestCase("none", 5, 5, false)]
    public void Health_Checks_Fail_On_The_Chosen_Level(string failOn, int errors, int warnings, bool fails)
        => Assert.That(HealthCheckRunAllCommand.ShouldFail(failOn, errors, warnings), Is.EqualTo(fails));

    [TestCase(RuntimeLevelModel.Upgrade, "upgrade")]
    [TestCase(RuntimeLevelModel.BootFailed, "failed to boot")]
    [TestCase(RuntimeLevelModel.Boot, "isn't running")]
    public void Status_Says_Why_The_Site_Is_Not_Running(RuntimeLevelModel level, string text)
        => Assert.That(ServerStatusCommand.Describe(level), Does.Contain(text));
}
