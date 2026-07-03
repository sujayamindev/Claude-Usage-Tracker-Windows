using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Ported from the macOS app's ClaudeAPIService. Mirrors the response parsing in
/// ClaudeAPIService.parseUsageResponse. Session-key requests are delegated to IClaudeApiTransport
/// (see WebView2ApiTransport / spec addendum for why claude.ai can't be called via a plain
/// HttpClient) — this class owns parsing and validation for that path. The CLI OAuth fallback
/// (FetchUsageDataViaCliOAuthAsync) is a separate path that calls api.anthropic.com directly via
/// a plain HttpClient instead, since that endpoint isn't expected to be behind the same
/// Cloudflare bot-fingerprinting as claude.ai.
/// </summary>
public sealed class ClaudeApiClient(IClaudeApiTransport transport, HttpClient? cliHttpClient = null)
{
    private static readonly Uri CliOAuthMessagesUri = new("https://api.anthropic.com/v1/messages");
    private readonly HttpClient _cliHttpClient = cliHttpClient ?? new HttpClient();

    public async Task<List<AccountInfo>> FetchOrganizationsAsync(
        string sessionKey, CancellationToken cancellationToken = default)
    {
        var response = await transport.GetAsync("/organizations", sessionKey, cancellationToken);
        EnsureSuccess(response);

        var organizations = JsonSerializer.Deserialize<List<AccountInfo>>(response.Body);
        if (organizations is null || organizations.Count == 0)
            throw new ClaudeApiException("No organizations found for this account");

        return organizations;
    }

    public async Task<ClaudeUsage> FetchUsageDataAsync(
        string sessionKey, string organizationId, CancellationToken cancellationToken = default)
    {
        var response = await transport.GetAsync($"/organizations/{organizationId}/usage", sessionKey, cancellationToken);
        EnsureSuccess(response);
        return ParseUsageResponse(response.Body);
    }

    /// <summary>
    /// Fetches usage via Claude Code CLI's OAuth token. There is no dedicated usage-JSON endpoint
    /// for this auth type (api.anthropic.com/api/oauth/usage is disabled) — this ports the macOS
    /// app's actual current approach: make a minimal, real Messages API call and read usage out
    /// of its rate-limit response headers. This means no Opus/Sonnet breakdown is available here
    /// (those headers don't exist) — ParseUsageFromRateLimitHeaders always zeroes them, matching
    /// macOS. Uses a plain HttpClient rather than WebView2ApiTransport: the Cloudflare
    /// bot-fingerprinting problem documented for claude.ai is not expected to apply to Anthropic's
    /// public developer API, and this needs to be verified manually (see the design spec).
    /// </summary>
    public async Task<ClaudeUsage> FetchUsageDataViaCliOAuthAsync(
        string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, CliOAuthMessagesUri);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = 1,
            messages = new[] { new { role = "user", content = "hi" } }
        });

        HttpResponseMessage response;
        try
        {
            response = await _cliHttpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ClaudeApiException($"Failed to reach Anthropic API: {ex.Message}");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ClaudeApiException(
                    $"CLI OAuth token rejected ({(int)response.StatusCode})", isUnauthorized: true);
            }

            if (!response.IsSuccessStatusCode)
                throw new ClaudeApiException($"Anthropic API returned {(int)response.StatusCode}");

            return ParseUsageFromRateLimitHeaders(response);
        }
    }

    private static ClaudeUsage ParseUsageFromRateLimitHeaders(HttpResponseMessage response)
    {
        double? HeaderDouble(string name) =>
            response.Headers.TryGetValues(name, out var values) &&
            double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

        var sessionUtilization = HeaderDouble("anthropic-ratelimit-unified-5h-utilization") ?? 0.0;
        var sessionPercentage = sessionUtilization * 100.0;

        var sessionResetSeconds = HeaderDouble("anthropic-ratelimit-unified-5h-reset") ?? 0.0;
        var sessionResetTime = sessionResetSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)sessionResetSeconds)
            : DateTimeOffset.Now.AddHours(5);

        if (sessionResetTime < DateTimeOffset.Now)
            sessionPercentage = 0.0;

        var weeklyUtilization = HeaderDouble("anthropic-ratelimit-unified-7d-utilization") ?? 0.0;
        var weeklyPercentage = weeklyUtilization * 100.0;

        var weeklyResetSeconds = HeaderDouble("anthropic-ratelimit-unified-7d-reset") ?? 0.0;
        var weeklyResetTime = weeklyResetSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)weeklyResetSeconds)
            : DateTimeOffset.Now.AddDays(7);

        return new ClaudeUsage
        {
            SessionPercentage = sessionPercentage,
            SessionResetTime = sessionResetTime,
            WeeklyPercentage = weeklyPercentage,
            WeeklyResetTime = weeklyResetTime,
            OpusWeeklyPercentage = 0.0,
            SonnetWeeklyPercentage = 0.0,
            SonnetWeeklyResetTime = null,
            LastUpdated = DateTimeOffset.Now
        };
    }

    private static void EnsureSuccess(ApiResponse response)
    {
        if (response.StatusCode is >= 200 and < 300)
            return;

        var preview = response.Body.Length > 200 ? response.Body[..200] : response.Body;

        if (response.StatusCode is 401 or 403)
        {
            throw new ClaudeApiException(
                $"Session key rejected ({response.StatusCode}): {preview}", isUnauthorized: true);
        }

        throw new ClaudeApiException($"Claude API returned {response.StatusCode}: {preview}");
    }

    private static ClaudeUsage ParseUsageResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sessionResetTime = DateTimeOffset.Now.AddHours(5);
        var sessionPercentage = 0.0;
        if (root.TryGetProperty("five_hour", out var fiveHour))
        {
            sessionPercentage = ParseUtilization(fiveHour);
            sessionResetTime = ParseResetsAt(fiveHour, sessionResetTime);
        }

        var weeklyResetTime = DateTimeOffset.Now.AddDays(7);
        var weeklyPercentage = 0.0;
        if (root.TryGetProperty("seven_day", out var sevenDay))
        {
            weeklyPercentage = ParseUtilization(sevenDay);
            weeklyResetTime = ParseResetsAt(sevenDay, weeklyResetTime);
        }

        var opusPercentage = 0.0;
        if (root.TryGetProperty("seven_day_opus", out var sevenDayOpus))
            opusPercentage = ParseUtilization(sevenDayOpus);

        var sonnetPercentage = 0.0;
        DateTimeOffset? sonnetResetTime = null;
        if (root.TryGetProperty("seven_day_sonnet", out var sevenDaySonnet))
        {
            sonnetPercentage = ParseUtilization(sevenDaySonnet);
            if (sevenDaySonnet.ValueKind == JsonValueKind.Object &&
                sevenDaySonnet.TryGetProperty("resets_at", out var resetsAt) &&
                resetsAt.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(resetsAt.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
            {
                sonnetResetTime = parsed;
            }
        }

        return new ClaudeUsage
        {
            SessionPercentage = sessionPercentage,
            SessionResetTime = sessionResetTime,
            WeeklyPercentage = weeklyPercentage,
            WeeklyResetTime = weeklyResetTime,
            OpusWeeklyPercentage = opusPercentage,
            SonnetWeeklyPercentage = sonnetPercentage,
            SonnetWeeklyResetTime = sonnetResetTime,
            LastUpdated = DateTimeOffset.Now
        };
    }

    /// <summary>Utilization may arrive as an int or a double depending on the endpoint. The period
    /// itself may be JSON null (e.g. no activity yet in that window) rather than an object.</summary>
    private static double ParseUtilization(JsonElement period)
    {
        if (period.ValueKind != JsonValueKind.Object || !period.TryGetProperty("utilization", out var utilization))
            return 0.0;

        return utilization.ValueKind switch
        {
            JsonValueKind.Number => utilization.GetDouble(),
            JsonValueKind.String when double.TryParse(
                utilization.GetString(), CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0.0
        };
    }

    private static DateTimeOffset ParseResetsAt(JsonElement period, DateTimeOffset fallback)
    {
        if (period.ValueKind == JsonValueKind.Object &&
            period.TryGetProperty("resets_at", out var resetsAt) &&
            resetsAt.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(resetsAt.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
