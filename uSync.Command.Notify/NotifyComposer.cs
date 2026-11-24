using Microsoft.Extensions.DependencyInjection;

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace uSync.Notify;

public class NotifyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<NotifyOptions>()
            .Bind(builder.Config.GetSection(NotifyOptions.Section));

        builder.WebhookEvents().Add<SiteStartedWebhookEvent>();

        builder.Services.AddTransient<NotifyTokenHandler>();
        builder.Services.AddHttpClient(Constants.HttpClients.WebhookFiring)
            .AddHttpMessageHandler<NotifyTokenHandler>();
    }
}
