namespace uSync.Notify;

/// <summary>
///  settings for the site started notification (appsettings uSync:Notify).
/// </summary>
public class NotifyOptions
{
    public const string Section = "uSync:Notify";

    /// <summary>
    ///  the commit / build this site is running. Default: the part after '+'
    ///  in the site assembly's informational version, which the .NET SDK
    ///  fills with the git commit when it builds in a git checkout.
    /// </summary>
    public string? Revision { get; set; }

    /// <summary>
    ///  name of this environment, passed in the payload so a pipeline that deploys
    ///  the same build to more than one site can tell them apart.
    ///  Default: the ASP.NET Core environment name.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    ///  the event_type sent in the payload (what the GitHub workflow listens for).
    /// </summary>
    public string EventType { get; set; } = "usync-site-started";

    /// <summary>
    ///  named secrets a webhook can ask for with the uSync-Notify-Token header.
    ///  set them as environment variables, e.g. uSync__Notify__Secrets__GitHubToken.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = [];

    /// <summary>
    ///  hosts a secret may be sent to. Stops someone with webhook access in
    ///  the backoffice sending a secret to a server of their own.
    /// </summary>
    public string[] AllowedHosts { get; set; } = ["api.github.com"];
}
