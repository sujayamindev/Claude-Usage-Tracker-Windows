namespace ClaudeUsageTracker.Windows.Services;

public readonly record struct ApiResponse(int StatusCode, string Body);

/// <summary>
/// Fetches a claude.ai/api path with the given session key attached as a cookie. Decoupled from
/// ClaudeApiClient so its parsing/validation logic stays unit-testable independent of how the
/// request is actually made (see WebView2ApiTransport for why this can't be a plain HttpClient).
/// </summary>
public interface IClaudeApiTransport
{
    Task<ApiResponse> GetAsync(string path, string sessionKey, CancellationToken cancellationToken = default);
}
