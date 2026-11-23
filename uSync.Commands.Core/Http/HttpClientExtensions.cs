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
        // BaseAddress can't be changed once the client has sent a request,
        // and we ask for new tokens part way through long running commands.
        client.BaseAddress ??= auth.Url;
        var address = $"{auth.Url}umbraco/management/api/v1/security/back-office/token";
        var tokenResponse = await client.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = address,
                ClientId = auth.ClientId,
                ClientSecret = auth.ClientSecret,
            });

        if (tokenResponse.IsError is false && tokenResponse.AccessToken is not null)
            return tokenResponse.AccessToken;

        if (IsCredentialRejection(tokenResponse))
            throw new SyncAuthenticationException(CredentialRejectionMessage(auth, tokenResponse));

        if (tokenResponse.ErrorType == ResponseErrorType.Exception && tokenResponse.Exception is not null)
            throw new HttpRequestException($"Could not get an access token from {auth.Url}: {tokenResponse.Exception.Message}", tokenResponse.Exception);

        throw new HttpRequestException($"Could not get an access token from {auth.Url}: {DescribeError(tokenResponse)}");
    }

    /// <summary>
    ///  the site answered the token request, but said no to the client id / secret.
    /// </summary>
    /// <remarks>
    ///  Umbraco's token endpoint answers a wrong secret with a 401 (and sometimes
    ///  a 400 invalid_client) that may not have an OAuth error in the body, so
    ///  the status code is what counts.
    /// </remarks>
    public static bool IsCredentialRejection(TokenResponse response)
        => response.IsError
            && response.ErrorType is ResponseErrorType.Protocol or ResponseErrorType.Http
            && (response.HttpStatusCode is System.Net.HttpStatusCode.BadRequest
                    or System.Net.HttpStatusCode.Unauthorized
                    or System.Net.HttpStatusCode.Forbidden
                || response.Error is "invalid_client" or "unauthorized_client");

    public static string CredentialRejectionMessage(SyncConnectionParameters auth, TokenResponse response)
        => $"The site is up but rejected the client id/secret ({DescribeError(response)}). Check the client id '{auth.ClientId}' and its secret, and that the API user exists on {auth.Url}";

    private static string DescribeError(TokenResponse response)
    {
        var status = $"{(int)response.HttpStatusCode} {response.HttpStatusCode}";
        return string.IsNullOrWhiteSpace(response.Error) ? status : $"{status}, {response.Error}";
    }

}
