using Microsoft.Extensions.DependencyInjection;

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

using uSync.Commands.Core;
using uSync.Commands.Server.Configuration;
using uSync.Commands.Server.Services;
using uSync.Platform.Command.Server.Auth;

namespace uSync.Commands.Server;

public class SyncCommandComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AdduSyncCommands();
    }
}

internal static class CommandServerBootExtensions
{
    public static IUmbracoBuilder AdduSyncCommands(this IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<SyncCommandConfiguration>()
            .Bind(builder.Config.GetSection(SyncCommandServer.SettingsPath));

        builder.WithCollectionBuilder<SyncCommandCollectionBuilder>()
            .Add(builder.TypeLoader.GetTypes<ISyncCommand>());

        builder.Services.AddSingleton<ISyncCommandService, SyncCommandService>();

        builder.Services.AddAuthentication(o =>
               o.AddScheme(SyncCommandServer.AuthScheme,
               a => a.HandlerType = typeof(SyncCommandAuthenticationHandler)));

        return builder;
    }
}
