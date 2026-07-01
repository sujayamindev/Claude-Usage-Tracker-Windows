using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsageTracker.Windows.Services;

public enum ClaudeStatusIndicator
{
    None,
    Minor,
    Major,
    Critical,
    Unknown
}

public sealed record ClaudeStatus(ClaudeStatusIndicator Indicator, string Description);

/// <summary>
/// Fetches live Claude system status from Statuspage.io's public API, ported from the macOS
/// app's ClaudeStatusService.swift. Uses a plain HttpClient — status.claude.com is a separate
/// public service, not behind the claude.ai Cloudflare bot-check that requires WebView2Api.
/// </summary>
public sealed class ClaudeStatusService : IDisposable
{
    private static readonly Uri StatusUrl = new("https://status.claude.com/api/v2/status.json");

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<ClaudeStatus> FetchStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<StatusResponse>(StatusUrl, cancellationToken)
            ?? throw new InvalidOperationException("Empty status response");

        var indicator = response.Status.Indicator switch
        {
            "none" => ClaudeStatusIndicator.None,
            "minor" => ClaudeStatusIndicator.Minor,
            "major" => ClaudeStatusIndicator.Major,
            "critical" => ClaudeStatusIndicator.Critical,
            _ => ClaudeStatusIndicator.Unknown
        };

        return new ClaudeStatus(indicator, response.Status.Description);
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed record StatusResponse([property: JsonPropertyName("status")] StatusDetail Status);

    private sealed record StatusDetail(
        [property: JsonPropertyName("indicator")] string Indicator,
        [property: JsonPropertyName("description")] string Description);
}
