using IdentityModel.Client;

using uSync.Commands.Core.Commands;

namespace uSync.Commands.Core.Http;
public static class HttpClientExtensions
{
    public static async Task<HttpClient> AuthorizeUmbracoClient(this HttpClient client, SyncConnectionParameters auth)
    {
        var token = await GetAccessToken(client, auth);
        if (token is not null)
            client.SetBearerToken(token);
        return client;
    }

    public static async Task<string?> GetAccessToken(this HttpClient client, SyncConnectionParameters auth)
    {
        client.BaseAddress = auth.Url;
        var address = $"{client.BaseAddress}umbraco/management/api/v1/security/back-office/token";
        var tokenResponse = await client.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = address,
                ClientId = auth.ClientId,
                ClientSecret = auth.ClientSecret,
            });

        if (tokenResponse.IsError || tokenResponse.AccessToken is null)
        {
            throw new Exception("Error:" + tokenResponse.Error);
        }
        return tokenResponse.AccessToken;
    }

}
