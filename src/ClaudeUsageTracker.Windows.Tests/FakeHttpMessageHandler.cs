using System.Net;
using System.Net.Http;

namespace ClaudeUsageTracker.Windows.Tests;

public sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };

        return Task.FromResult(response);
    }
}
