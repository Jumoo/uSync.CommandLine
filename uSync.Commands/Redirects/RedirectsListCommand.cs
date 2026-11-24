using Microsoft.Extensions.Configuration;

using System.CommandLine;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Redirects;

/// <summary>
///  whether redirect tracking is on, and the redirects the site holds.
/// </summary>
public class RedirectsListCommand : ConnectedCommandBase, ISyncCommand
{
    public const int DefaultTake = 100;

    private readonly Option<string?> _filter = new("--filter",
        "only redirects whose original url contains this text");

    private readonly Option<int> _take = new("--take",
        () => DefaultTake, "the most redirects to list");

    public Command Command { get; }

    public RedirectsListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("redirects-list", "Show whether redirect tracking is on, and list the redirects")
        {
            _filter, _take
        };
        AddCoreOptions(Command);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var cancel = context.GetCancellationToken();
            var take = Math.Max(0, context.ParseResult.GetValueForOption(_take));

            var status = await umbracoClient.GetRedirectManagementStatusAsync(cancel);
            await Writer.WriteLineAsync($"Redirect tracking: {(status.Status == RedirectStatusModel.Enabled ? "enabled" : "disabled")}");

            var redirects = await umbracoClient.GetRedirectManagementAsync(
                context.ParseResult.GetValueForOption(_filter) ?? string.Empty, 0, take, cancel);

            await Writer.WriteLineAsync($"Redirects: {redirects.Total}");
            foreach (var redirect in redirects.Items)
            {
                var culture = string.IsNullOrWhiteSpace(redirect.Culture) ? "" : $" [{redirect.Culture}]";
                await Writer.WriteLineAsync($"  {redirect.OriginalUrl} -> {redirect.DestinationUrl}{culture} ({redirect.Created.LocalDateTime:yyyy-MM-dd})");
            }

            if (redirects.Total > redirects.Items.Count)
                await Writer.WriteLineAsync($"  ... and {redirects.Total - redirects.Items.Count} more (use --take or --filter)");

            context.ExitCode = 0;
        });
    }
}
