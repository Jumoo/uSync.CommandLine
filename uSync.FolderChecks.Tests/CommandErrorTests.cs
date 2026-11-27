using Microsoft.Extensions.Configuration;

using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net;
using System.Text;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Http;
using uSync.Commands.uSync;

namespace uSync.FolderChecks.Tests;

/// <summary>
///  errors from connected commands: one line, the right exit code, and
///  credentials rejected by a running site are reported at once.
/// </summary>
public class CommandErrorTests
{
    /// <summary>
    ///  answers every request with the same status and body, like a site's token endpoint.
    /// </summary>
    private class FixedResponseHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static readonly SyncConnectionParameters Auth = new()
    {
        Url = new Uri("https://site.example.com/"),
        ClientId = "umbraco-back-office-test",
        ClientSecret = "wrong-secret",
    };

    private static readonly Dictionary<string, string?> Environment = new()
    {
        ["USYNC_SERVER"] = Auth.Url.ToString(),
        ["USYNC_CLIENT_ID"] = Auth.ClientId,
        ["USYNC_CLIENT_SECRET"] = Auth.ClientSecret,
    };

    [TestCase(HttpStatusCode.Unauthorized, "")]
    [TestCase(HttpStatusCode.BadRequest, """{ "error": "invalid_client", "error_description": "The client credentials are invalid." }""")]
    [TestCase(HttpStatusCode.Forbidden, "")]
    public void Ping_Fails_At_Once_When_The_Site_Rejects_The_Credentials(HttpStatusCode status, string body)
    {
        var handler = new FixedResponseHandler(status, body);
        var output = new StringWriter();
        var command = new uSyncPingCommand(output, new ConfigurationBuilder().Build(), new HttpClient(handler))
        {
            GetEnvironmentVariable = name => Environment.TryGetValue(name, out var value) ? value : null
        };

        var timer = Stopwatch.StartNew();
        var exit = new Parser(command.Command).Invoke(["--timeout", "60"]);

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(handler.Calls, Is.EqualTo(1));
        Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
        Assert.That(output.ToString(), Does.Contain("The site is up but rejected the client id/secret"));
        Assert.That(output.ToString(), Does.Not.Contain("never came up"));
        Assert.That(output.ToString(), Does.Not.Contain(Auth.ClientSecret));
    }

    [Test]
    public void Ping_Keeps_Trying_When_The_Site_Is_Starting()
    {
        var handler = new FixedResponseHandler(HttpStatusCode.ServiceUnavailable);
        var output = new StringWriter();
        var command = new uSyncPingCommand(output, new ConfigurationBuilder().Build(), new HttpClient(handler))
        {
            GetEnvironmentVariable = name => Environment.TryGetValue(name, out var value) ? value : null
        };

        var exit = new Parser(command.Command).Invoke(["--timeout", "60", "--retrys", "2"]);

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(handler.Calls, Is.EqualTo(2));
        Assert.That(output.ToString(), Does.Contain("never came up"));
    }

    [Test]
    public async Task Rejected_Credentials_Throw_An_Authentication_Error()
    {
        var client = new HttpClient(new FixedResponseHandler(HttpStatusCode.Unauthorized));

        var ex = Assert.ThrowsAsync<SyncAuthenticationException>(() => client.GetAccessToken(Auth));
        Assert.That(ex!.Message, Does.Contain("rejected the client id/secret").And.Contain("401"));
        Assert.That(ex.Message, Does.Not.Contain(Auth.ClientSecret));
        await Task.CompletedTask;
    }

    [Test]
    public void Server_Errors_Are_Not_Credential_Errors()
    {
        var client = new HttpClient(new FixedResponseHandler(HttpStatusCode.InternalServerError));
        Assert.ThrowsAsync<HttpRequestException>(() => client.GetAccessToken(Auth));
    }

    [Test]
    public void Usage_Errors_Exit_2_Without_A_Stack_Trace()
    {
        var error = new StringWriter();
        var exit = SyncCommandErrors.Handle(new SyncUsageException("No client id"), error, _ => null);

        Assert.That(exit, Is.EqualTo(2));
        Assert.That(error.ToString().Trim(), Is.EqualTo("ERROR: No client id"));
    }

    [Test]
    public void Run_Time_Errors_Exit_1_With_One_Line()
    {
        var error = new StringWriter();
        var ex = new InvalidOperationException("The HTTP status code of the response was not expected (500).\n\nStatus: 500\nResponse: <html>...");
        var exit = SyncCommandErrors.Handle(new AggregateException(ex), error, _ => null);

        Assert.That(exit, Is.EqualTo(1));
        Assert.That(error.ToString(), Does.StartWith("ERROR: The HTTP status code of the response was not expected (500)."));
        Assert.That(error.ToString(), Does.Not.Contain("Response:").And.Not.Contain(" at "));
        Assert.That(error.ToString(), Does.Contain("USYNC_DEBUG=1"));
    }

    [Test]
    public void Authentication_Errors_Exit_1()
    {
        var exit = SyncCommandErrors.Handle(new SyncAuthenticationException("rejected"), new StringWriter(), _ => null);
        Assert.That(exit, Is.EqualTo(1));
    }

    [Test]
    public void Debug_Shows_The_Stack_Trace()
    {
        var error = new StringWriter();
        try { throw new InvalidOperationException("boom"); }
        catch (Exception ex) { SyncCommandErrors.Handle(ex, error, name => name == "USYNC_DEBUG" ? "1" : null); }

        Assert.That(error.ToString(), Does.Contain("ERROR: boom").And.Contain("Debug_Shows_The_Stack_Trace"));
    }

    [Test]
    public void Bad_Client_Id_Is_A_Usage_Error()
    {
        var command = new uSyncPingCommand(TextWriter.Null, new ConfigurationBuilder().Build(), new HttpClient(new FixedResponseHandler(HttpStatusCode.OK)))
        {
            GetEnvironmentVariable = name => name == "USYNC_CLIENT_ID" ? "my-client" : Environment.TryGetValue(name, out var value) ? value : null
        };

        var parse = new Parser(command.Command).Parse([]);
        var ex = Assert.Throws<SyncUsageException>(() => command.GetConnectionPartameters(new System.CommandLine.Invocation.InvocationContext(parse)));
        Assert.That(ex!.Message, Does.Contain("umbraco-back-office"));
    }
}
