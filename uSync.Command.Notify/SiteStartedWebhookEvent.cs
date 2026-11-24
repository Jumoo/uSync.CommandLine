using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Reflection;

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Webhooks;

namespace uSync.Notify;

/// <summary>
///  fires when the site starts running a build it hasn't announced before,
///  so a deploy pipeline can carry on (import, report, health checks)
///  without a runner sitting there waiting for the site to come up.
/// </summary>
/// <remarks>
///  the payload is GitHub repository_dispatch shaped ({ event_type, client_payload })
///  so the webhook can point straight at the GitHub API. Other receivers can
///  read it as plain JSON.
/// </remarks>
[WebhookEvent("uSync Site Started")]
public class SiteStartedWebhookEvent : WebhookEventBase<UmbracoApplicationStartedNotification>
{
    public const string EventAlias = "uSyncSiteStarted";
    internal const string LastRevisionKey = "uSync.Command.Notify.LastRevision";

    private readonly IRuntimeState _runtimeState;
    private readonly IKeyValueService _keyValueService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<NotifyOptions> _options;
    private readonly ILogger<SiteStartedWebhookEvent> _logger;

    public SiteStartedWebhookEvent(
        IWebhookFiringService webhookFiringService,
        IWebhookService webhookService,
        IOptionsMonitor<WebhookSettings> webhookSettings,
        IServerRoleAccessor serverRoleAccessor,
        IRuntimeState runtimeState,
        IKeyValueService keyValueService,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<NotifyOptions> options,
        ILogger<SiteStartedWebhookEvent> logger)
        : base(webhookFiringService, webhookService, webhookSettings, serverRoleAccessor)
    {
        _runtimeState = runtimeState;
        _keyValueService = keyValueService;
        _hostEnvironment = hostEnvironment;
        _options = options;
        _logger = logger;
    }

    public override string Alias => EventAlias;

    public override bool ShouldFireWebhookForNotification(UmbracoApplicationStartedNotification notification)
    {
        // installing or upgrading: the pipeline can't import yet.
        if (_runtimeState.Level != RuntimeLevel.Run) return false;

        // an app pool recycle or a scale out restarts the site too, only
        // tell the pipeline about a build we haven't told it about before.
        var revision = GetRevision();
        if (revision is null) return true;

        return string.Equals(revision, _keyValueService.GetValue(LastRevisionKey), StringComparison.OrdinalIgnoreCase) is false;
    }

    public override async Task ProcessWebhooks(UmbracoApplicationStartedNotification notification, IEnumerable<IWebhook> webhooks, CancellationToken cancellationToken)
    {
        var enabled = webhooks.Where(x => x.Enabled).ToList();
        if (enabled.Count == 0) return;

        await base.ProcessWebhooks(notification, enabled, cancellationToken);

        var revision = GetRevision();
        if (revision is not null)
        {
            _keyValueService.SetValue(LastRevisionKey, revision);
        }

        _logger.LogInformation("uSync site started: queued {count} webhook(s) for revision {revision}", enabled.Count, revision ?? "(unknown)");
    }

    public override object? ConvertNotificationToRequestPayload(UmbracoApplicationStartedNotification notification)
    {
        var options = _options.CurrentValue;

        // property names are camel cased by Umbraco's serializer, which leaves these alone.
        return new
        {
            event_type = options.EventType,
            client_payload = new
            {
                revision = GetRevision(),
                environment = string.IsNullOrWhiteSpace(options.Environment) ? _hostEnvironment.EnvironmentName : options.Environment,
                machine = System.Environment.MachineName,
                startedUtc = DateTime.UtcNow,
            }
        };
    }

    private string? GetRevision()
    {
        var configured = _options.CurrentValue.Revision;
        if (string.IsNullOrWhiteSpace(configured) is false) return configured.Trim();

        return RevisionFromVersion(Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion);
    }

    /// <summary>
    ///  "1.0.0+4f1c2e..." => "4f1c2e..."
    /// </summary>
    /// <remarks>
    ///  when the version already has build metadata the SDK adds the commit
    ///  with a dot: "1.0.0+build.5.{sha}". A full commit hash at the end wins,
    ///  else everything after the '+'.
    /// </remarks>
    internal static string? RevisionFromVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion)) return null;

        var plus = informationalVersion.IndexOf('+');
        if (plus < 0 || plus == informationalVersion.Length - 1) return null;

        var metadata = informationalVersion[(plus + 1)..].Trim();
        var last = metadata[(metadata.LastIndexOf('.') + 1)..];
        return IsCommitHash(last) ? last : metadata;
    }

    private static bool IsCommitHash(string value)
        => value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);
}
