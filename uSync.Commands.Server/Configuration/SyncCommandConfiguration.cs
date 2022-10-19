using Umbraco.Cms.Core;

namespace uSync.Commands.Server.Configuration;
internal class SyncCommandConfiguration
{
    public string Enabled { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string UserId { get; set; } = Constants.Security.SuperUserIdAsString;
}
