using System.Net;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Answers every HTTP request with one body and keeps what was sent.</summary>
public sealed class StubHttpHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public Uri? Address { get; private set; }

    public string Sent { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Address = request.RequestUri;
        Sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(body) };
    }
}
