
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Diagnostics.Metrics;

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Security;

namespace uSync.Command.Setup;

public class CommandSetupComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, CommandApplicationStartedHandler>();
    }
}

internal class CommandApplicationStartedHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandApplicationStartedHandler> _logger;
    private readonly IRuntimeState _runtimeState;

    public CommandApplicationStartedHandler(
        IConfiguration configuration,
        IUserService userService,
        ILogger<CommandApplicationStartedHandler> logger,
        IRuntimeState runtimeState,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _userService = userService;
        _logger = logger;
        _runtimeState = runtimeState;
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (_runtimeState.Level != RuntimeLevel.Run)
            {
                _logger.LogInformation("uSync.Command.Setup skipping clientId/Secret user creation - runtime level is {level}", _runtimeState.Level);
                return;
            }
            if (notification.IsRestarting) return;

            await AddClientIdAndSecret();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error adding clientId/Secret user for uSync.Commands");
        }
    }

    private async Task AddClientIdAndSecret() 
    {
        if (_configuration.GetValue("uSync:Command:AddIfMissing", false) is false)
        {
            _logger.LogInformation("uSync.Command.Setup is disabled, not adding clientId/Secret user");
            return; // we require the explicit setup value to be true, to do this (off by default)
        }

        var clientId = _configuration.GetValue("uSync:Command:ClientId", string.Empty);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("uSync.Command.Setup is enabled, but the config contains no clientId");
            return; // no client id in the config
        }

        var clientSecret = _configuration.GetValue("uSync:Command:Secret", string.Empty);
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogWarning("uSync.Command.Setup is enabled, but the config contains no clientSecret");
            return; // no client secret in the config
        }

        var user = await _userService.FindByClientIdAsync(clientId);
        if (user is not null) return; // already setup

        var defaultName = $"{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}@jumoo.co.uk";

        // create the user
        HashSet<Guid> groups = [_configuration.GetValue("uSync:Command:UserGroupKey", Constants.Security.AdminGroupKey)];

        var result = await _userService.CreateAsync(Constants.Security.SuperUserKey, new UserCreateModel
        {
            Email = _configuration.GetValue("uSync:Command:Email", defaultName),
            UserName = _configuration.GetValue("uSync:Command:Username", defaultName),
            Kind = Umbraco.Cms.Core.Models.Membership.UserKind.Api,
            Name = _configuration.GetValue("uSync:Command:Name", "uSync Command User"),
            UserGroupKeys = groups
        });

        if (result.Success is false)
        {
            _logger.LogWarning("uSync.Command.Setup was unable to create the user: {status}", result.Status);
            return; // didn't work. 
        }

        var userKey = result.Result.CreatedUser?.Key;
        if (userKey is null) {
            _logger.LogWarning("uSync.Command.Setup was unable to create the user: no user key returned");
            return;
        }

        // add the client id.
        var addClientIdResult = await _userService.AddClientIdAsync(userKey.Value, clientId);
        if (addClientIdResult != UserClientCredentialsOperationStatus.Success) {
            _logger.LogWarning("uSync.Command.Setup was unable to add the clientId to the user: {status}", addClientIdResult);
            return; // didn't work
        }


        // we can't inject this, because it will cause a failure on a clean boot if we do,
        // so when we are here, we know umbraco is installed, we can go fetch it. 
        var backOfficeApplicationManager = _serviceProvider.GetService<IBackOfficeApplicationManager>();
        if (backOfficeApplicationManager is null)
        {
            _logger.LogWarning("uSync.Command.Setup was unable to add the client secret to the user: no IBackOfficeApplicationManager");
            return;
        }

        // add the client secret
        await backOfficeApplicationManager.EnsureBackOfficeClientCredentialsApplicationAsync(clientId, clientSecret);
        _logger.LogInformation("uSync.Command.Setup has created the clientId/Secret user for uSync.Commands");

        await _userService.EnableAsync(userKey.Value, new HashSet<Guid> { userKey.Value });
    }
}
