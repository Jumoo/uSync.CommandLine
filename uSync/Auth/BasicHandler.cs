using System.Net.Http.Headers;
using System.Text;

namespace uSync.Auth;

internal class BasicHandler : DelegatingHandler
{
    private string _credentials;

    public BasicHandler(string? username, string? password)
    {
        _credentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username + ":" + password));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _credentials);
        return base.SendAsync(request, cancellationToken);
    }
}
