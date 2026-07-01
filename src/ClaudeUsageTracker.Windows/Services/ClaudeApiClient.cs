using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Ported from the macOS app's ClaudeAPIService. Session-key (cookie) auth only — the CLI OAuth
/// flow is out of scope for the MVP. Mirrors the response parsing in ClaudeAPIService.parseUsageResponse.
/// </summary>
public sealed class ClaudeApiClient(HttpClient httpClient)
{
    private const string BaseUrl = "https://claude.ai/api";

    public async Task<List<AccountInfo>> FetchOrganizationsAsync(
        string sessionKey, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/organizations", sessionKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var organizations = await response.Content.ReadFromJsonAsync<List<AccountInfo>>(
            cancellationToken: cancellationToken);

        if (organizations is null || organizations.Count == 0)
            throw new ClaudeApiException("No organizations found for this account");

        return organizations;
    }

    public async Task<ClaudeUsage> FetchUsageDataAsync(
        string sessionKey, string organizationId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/organizations/{organizationId}/usage", sessionKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseUsageResponse(json);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string sessionKey)
    {
        var request = new HttpRequestMessage(method, BaseUrl + path);
        request.Headers.Add("Cookie", $"sessionKey={sessionKey}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Referrer = new Uri("https://claude.ai");
        request.Headers.Add("Origin", "https://claude.ai");
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ClaudeApiException("Session key is invalid or expired", isUnauthorized: true);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var preview = body.Length > 200 ? body[..200] : body;
        throw new ClaudeApiException($"Claude API returned {(int)response.StatusCode}: {preview}");
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
            if (sevenDaySonnet.TryGetProperty("resets_at", out var resetsAt) &&
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

    /// <summary>Utilization may arrive as an int or a double depending on the endpoint.</summary>
    private static double ParseUtilization(JsonElement period)
    {
        if (!period.TryGetProperty("utilization", out var utilization))
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
        if (period.TryGetProperty("resets_at", out var resetsAt) &&
            resetsAt.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(resetsAt.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
