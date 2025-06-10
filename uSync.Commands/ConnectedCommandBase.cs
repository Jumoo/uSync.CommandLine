using IdentityModel.Client;

using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;
using uSync.Commands.Core.Http;
using uSync.Management.Api;

namespace uSync.Commands;
public class ConnectedCommandBase : SyncCommandBase
{
    protected readonly HttpClient Client;

    public ConnectedCommandBase(TextWriter writer, IConfiguration configuration, HttpClient client) 
        : base(writer, configuration)
    {
        this.Client = client;
    }

    protected async Task<UmbracoClient> GetUmbracoClient(InvocationContext context)
    {
        var auth = GetConnectionPartameters(context);
        Client.BaseAddress = auth.Url;
        await Client.AuthorizeUmbracoClient(auth);
        return new UmbracoClient(auth.Url.AbsoluteUri, Client);
    }

    protected Task<uSyncClient> GetUSyncClient(InvocationContext context, string? token)
    {
        var auth = GetConnectionPartameters(context);
        if (token is not null)
            Client.SetBearerToken(token);

        return Task.FromResult(new uSyncClient(auth.Url.AbsoluteUri, Client));
    }

    protected async Task<string> GetToken(InvocationContext context)
    {
        var auth = GetConnectionPartameters(context);
        Client.BaseAddress = auth.Url;
        var token = await Client.GetAccessToken(auth);
        if (token is null)
        {
            throw new Exception("Failed to get access token.");
        }
        return token;
    }
}

