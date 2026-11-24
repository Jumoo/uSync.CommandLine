using Microsoft.Extensions.Configuration;

using System.CommandLine;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Output;

namespace uSync.Commands.Webhooks;

/// <summary>
///  the webhooks on the site - e.g. to check the one files-webhook wrote was imported.
/// </summary>
public class WebhookListCommand : ConnectedCommandBase, ISyncCommand
{
    private readonly Option<bool> _json = JsonOutput.CreateOption();

    public Command Command { get; }

    public WebhookListCommand(
        TextWriter writer,
        IConfiguration configuration,
        HttpClient client) : base(writer, configuration, client)
    {
        Command = new Command("webhook-list", "List the webhooks on the server");
        AddCoreOptions(Command);
        Command.AddOption(_json);

        Command.SetHandler(async (context) =>
        {
            var umbracoClient = await GetUmbracoClient(context);
            var webhooks = await umbracoClient.GetWebhookAsync(0, 1000, context.GetCancellationToken());

            if (context.ParseResult.GetValueForOption(_json))
            {
                // header values can be secrets, so only the names - as in the text output.
                await JsonOutput.WriteAsync(Writer, new
                {
                    webhooks.Total,
                    Items = webhooks.Items.Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Description,
                        x.Enabled,
                        x.Url,
                        Events = x.Events.Select(e => e.EventName),
                        x.ContentTypeKeys,
                        Headers = x.Headers.Keys,
                    })
                });
                context.ExitCode = 0;
                return;
            }

            await Writer.WriteLineAsync($"Webhooks: {webhooks.Total}");
            foreach (var webhook in webhooks.Items)
            {
                var name = string.IsNullOrWhiteSpace(webhook.Name) ? webhook.Id.ToString() : webhook.Name;
                await Writer.WriteLineAsync($"  {name} ({(webhook.Enabled ? "enabled" : "disabled")})");
                await Writer.WriteLineAsync($"    url:    {webhook.Url}");
                await Writer.WriteLineAsync($"    events: {string.Join(", ", webhook.Events.Select(x => x.EventName))}");

                // header values can be secrets, so only the names.
                if (webhook.Headers.Count > 0)
                    await Writer.WriteLineAsync($"    headers: {string.Join(", ", webhook.Headers.Keys)}");
            }

            context.ExitCode = 0;
        });
    }
}
