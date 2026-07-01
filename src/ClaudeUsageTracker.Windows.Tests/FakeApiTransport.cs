using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public sealed class FakeApiTransport(int statusCode, string body) : IClaudeApiTransport
{
    public string? LastPath { get; private set; }
    public string? LastSessionKey { get; private set; }

    public Task<ApiResponse> GetAsync(string path, string sessionKey, CancellationToken cancellationToken = default)
    {
        LastPath = path;
        LastSessionKey = sessionKey;
        return Task.FromResult(new ApiResponse(statusCode, body));
    }
}
