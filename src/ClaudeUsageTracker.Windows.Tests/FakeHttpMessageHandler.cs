using System.Net;
using System.Net.Http;

namespace ClaudeUsageTracker.Windows.Tests;

/// <summary>Lets tests stub an HttpClient's response (status, headers, body) without a real network call.</summary>
public sealed class FakeHttpMessageHandler(
    HttpStatusCode statusCode, IReadOnlyDictionary<string, string>? headers = null, string body = "")
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;

        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(body) };
        if (headers is not null)
        {
            foreach (var (name, value) in headers)
                response.Headers.TryAddWithoutValidation(name, value);
        }

        return Task.FromResult(response);
    }
}
