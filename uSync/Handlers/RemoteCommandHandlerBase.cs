using System.CommandLine;

using Microsoft.Extensions.Logging;

using uSync.Services;

namespace uSync.Handlers;
public abstract class RemoteCommandHandlerBase : ISyncCommandHandler
{
    protected readonly ILogger _logger;
    public Command? Command { get; protected set; }

    protected RemoteCommandHandlerBase(ILogger logger)
    {
        _logger = logger;
    }


    protected Uri? ValidateServerUri(Uri? server)
    {
        if (server == null) return null;
        if (!server.IsAbsoluteUri) return null;
        return server;
    }


    // common options for remote commands 
    // these can then be added to a command 

    protected Option<Uri> optionServerName = new Option<Uri>(
        new[] { "--server", "-s" }, "Name or URL of server to connect to")
        { IsRequired = true };

    protected Option<string> optionAuthKey = new Option<string>(
        new[] { "--key", "-k" }, "AuthKey to use when connecting to the server");

    protected Option<string> optionUsername = new Option<string>(
        new[] { "--username", "-user" }, "Username to use when logging into server");

    protected Option<string> optionPassword = new Option<string>(
        new[] { "--password", "-pass" }, "Password to use when logging into server");


    protected void AddCoreOptions(Command command)
    {
        command.AddOption(optionServerName);
        command.AddOption(optionAuthKey);
        command.AddOption(optionUsername);
        command.AddOption(optionPassword);
    }



    public IRemoteRuntimeService EnsureRuntimeService(RemoteCommandParameters parameters)
    {
        if (parameters.ServerName == null)
            throw new uSyncCommandException(11, "Missing server parameter");

        var url = ValidateServerUri(parameters.ServerName);
        if (url == null)
            throw new uSyncCommandException(12, $"Failed to get URL for server {parameters.ServerName}");

        if (string.IsNullOrEmpty(parameters.AuthKey)
            && (string.IsNullOrEmpty(parameters.Username) || string.IsNullOrEmpty(parameters.Password)))
        {
            throw new uSyncCommandException(13, "Missing authentication key or username and password");
        }

        return !string.IsNullOrEmpty(parameters.AuthKey)
            ? new RemoteRuntimeService(_logger, url, parameters.AuthKey)
            : new RemoteRuntimeService(_logger, url, parameters.Username, parameters.Password);

    }
}

public class RemoteCommandParameters
{
    public Uri? ServerName { get; init; }
    public string? AuthKey { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}
