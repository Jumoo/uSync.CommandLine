using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace uSync.Auth;
internal class HmacHandler : DelegatingHandler
{
    private string _key { get; set; }
    public HmacHandler(string hmacKey)
    {
        _key = hmacKey;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString();

        var contentLength = 0;

        if (request.Content != null)
        {
            byte[] content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentLength = content.Length;
        }

        string token = $"{request.Method}{timestamp}{nonce}{contentLength}";

        var secretBytes = Convert.FromBase64String(_key);
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        using (HMACSHA256 hmac = new HMACSHA256(secretBytes))
        {
            var hashed = hmac.ComputeHash(tokenBytes);
            string stringToken = Convert.ToBase64String(hashed);
            request.Headers.Authorization = new AuthenticationHeaderValue("hmacauth", $"{stringToken}:{nonce}:{timestamp}");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}