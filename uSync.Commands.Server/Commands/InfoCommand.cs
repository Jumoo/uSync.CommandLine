using Microsoft.Extensions.Hosting;

using Umbraco.Cms.Core.Services;

using uSync.Commands.Core;

namespace uSync.Commands.Server.Commands;
internal class InfoCommand : SyncCommandBase
{
    public override string Id => "Info";
    public override string Name => "Information";
    public override string Description => "Information about the Umbraco installation";


    private readonly IRuntimeState _runtimeState;
    private readonly IServerRegistrationService _serverRegistrationService;
    private readonly IHostEnvironment _hostEnvironment;

    public InfoCommand(
        IRuntimeState runtimeState,
        IServerRegistrationService serverRegistrationService,
        IHostEnvironment hostEnvironment)
    {
        _runtimeState = runtimeState;
        _serverRegistrationService = serverRegistrationService;
        _hostEnvironment = hostEnvironment;
    }

    public override Task<SyncCommandResponse> Execute(SyncCommandRequest request)
        => Task.FromResult(new SyncCommandResponse(request.Id, true)
        {
            ResultType = SyncCommandObjectType.Json,
            Result = new
            {
                Version = _runtimeState.Version.ToString(),
                Level = _runtimeState.Level.ToString(),
                Role = _serverRegistrationService.GetCurrentServerRole(),
                Servers = string.Join(" ", _serverRegistrationService.GetActiveServers()?.Select(x => x.ServerAddress) ?? Enumerable.Empty<string>()),
                Environment = _hostEnvironment.EnvironmentName,
                ApplicatioName = _hostEnvironment.ApplicationName,
                ContentRootPath = _hostEnvironment.ContentRootPath,
                Parameters = string.Join(",", request.Parameters.Select(x => x.Id))
            }
        }); 
}
