using System.Reflection;

namespace uSync.Commands.Core.Commands;

/// <summary>
///  the command was used wrongly or isn't configured (missing option, bad client id)
///  - exits 2, like a bad argument.
/// </summary>
public class SyncUsageException(string message) : Exception(message);

/// <summary>
///  the site answered but wouldn't give us an access token (wrong client id or secret)
///  - exits 1.
/// </summary>
public class SyncAuthenticationException(string message) : Exception(message);

/// <summary>
///  turns an exception that escaped a command into a one line error and an exit code.
/// </summary>
/// <remarks>
///  exit 2 for usage / configuration problems, 1 for everything that went wrong
///  at run time. Set USYNC_DEBUG=1 to see the stack trace.
/// </remarks>
public static class SyncCommandErrors
{
    public const int ExitFailed = 1;
    public const int ExitUsage = 2;

    public const string DebugEnvironmentVariable = "USYNC_DEBUG";

    public static int Handle(Exception exception, TextWriter error, Func<string, string?>? getEnvironment = null)
    {
        var ex = Unwrap(exception);

        if (ex is OperationCanceledException)
        {
            error.WriteLine("Cancelled.");
            return ExitFailed;
        }

        error.WriteLine($"ERROR: {FirstLine(ex.Message)}");

        var debug = (getEnvironment ?? Environment.GetEnvironmentVariable)(DebugEnvironmentVariable);
        if (debug is "1" || string.Equals(debug, "true", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine(ex.ToString());
        }
        else if (ex is not (SyncUsageException or SyncAuthenticationException))
        {
            error.WriteLine($"   (set {DebugEnvironmentVariable}=1 for the full error)");
        }

        return ex is SyncUsageException ? ExitUsage : ExitFailed;
    }

    private static Exception Unwrap(Exception exception)
    {
        var ex = exception;
        while (true)
        {
            if (ex is TargetInvocationException { InnerException: not null } tie) ex = tie.InnerException!;
            else if (ex is AggregateException { InnerExceptions.Count: 1 } agg) ex = agg.InnerExceptions[0];
            else return ex;
        }
    }

    // generated api client errors put the whole response body after the first line.
    private static string FirstLine(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(line) ? message.Trim() : line;
    }
}
