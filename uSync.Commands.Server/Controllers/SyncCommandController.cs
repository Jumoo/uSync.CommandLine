using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;

using uSync.Commands.Core;
using uSync.Commands.Server.Services;

namespace uSync.Commands.Server.Controllers;

[PluginController("uSync")]
[Authorize(AuthenticationSchemes = SyncCommandServer.AuthScheme)]
public class SyncCommandController : UmbracoApiController
{
    private readonly ISyncCommandService _commandService;

    public SyncCommandController(ISyncCommandService commandService)
    {
        _commandService = commandService;
    }

    [HttpGet]
    public IReadOnlyCollection<SyncCommandInfo> List(string command)
    {
        var commands = _commandService.ListCommands()
            .OrderBy(x => x.Id).ToList();

        if (!string.IsNullOrWhiteSpace(command))
            return commands
                .Where(x => x.Id != null && x.Id.StartsWith(command, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return commands;
    }

    [HttpPost]
    public async Task<SyncCommandResponse> Execute(SyncCommandRequest request)
    {
        if (request?.CommandId == null)
            throw new ArgumentNullException(nameof(request.CommandId));

        return await _commandService.Execute(request.CommandId, request);
    }
}
