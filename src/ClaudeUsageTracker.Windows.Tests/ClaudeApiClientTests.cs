using System.Net;
using System.Net.Http;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class ClaudeApiClientTests
{
    private const string UsageResponseJson = """
    {
        "five_hour": { "utilization": 42, "resets_at": "2026-07-01T12:00:00.000Z" },
        "seven_day": { "utilization": 18.5, "resets_at": "2026-07-07T12:00:00.000Z" },
        "seven_day_opus": { "utilization": 5 },
        "seven_day_sonnet": { "utilization": 13, "resets_at": "2026-07-07T12:00:00.000Z" }
    }
    """;

    [Fact]
    public async Task FetchUsageDataAsync_ParsesAllFields()
    {
        var transport = new FakeApiTransport(200, UsageResponseJson);
        var client = new ClaudeApiClient(transport);

        var usage = await client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123");

        Assert.Equal(42, usage.SessionPercentage);
        Assert.Equal(18.5, usage.WeeklyPercentage);
        Assert.Equal(5, usage.OpusWeeklyPercentage);
        Assert.Equal(13, usage.SonnetWeeklyPercentage);
        Assert.NotNull(usage.SonnetWeeklyResetTime);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), usage.SessionResetTime);
        Assert.Equal(new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero), usage.WeeklyResetTime);
    }

    [Fact]
    public async Task FetchUsageDataAsync_TreatsExplicitNullPeriodsAsZero()
    {
        const string json = """
        {
            "five_hour": null,
            "seven_day": null,
            "seven_day_opus": null,
            "seven_day_sonnet": null
        }
        """;
        var transport = new FakeApiTransport(200, json);
        var client = new ClaudeApiClient(transport);

        var usage = await client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123");

        Assert.Equal(0, usage.SessionPercentage);
        Assert.Equal(0, usage.WeeklyPercentage);
        Assert.Equal(0, usage.OpusWeeklyPercentage);
        Assert.Equal(0, usage.SonnetWeeklyPercentage);
        Assert.Null(usage.SonnetWeeklyResetTime);
    }

    [Fact]
    public async Task FetchUsageDataAsync_DefaultsMissingFieldsToZero()
    {
        var transport = new FakeApiTransport(200, "{}");
        var client = new ClaudeApiClient(transport);

        var usage = await client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123");

        Assert.Equal(0, usage.SessionPercentage);
        Assert.Equal(0, usage.WeeklyPercentage);
        Assert.Null(usage.SonnetWeeklyResetTime);
    }

    [Fact]
    public async Task FetchUsageDataAsync_PassesPathAndSessionKeyToTransport()
    {
        var transport = new FakeApiTransport(200, UsageResponseJson);
        var client = new ClaudeApiClient(transport);

        await client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123");

        Assert.Equal("/organizations/org-123/usage", transport.LastPath);
        Assert.Equal("sk-ant-sid01-test-key", transport.LastSessionKey);
    }

    [Fact]
    public async Task FetchUsageDataAsync_ThrowsUnauthorizedOn401()
    {
        var transport = new FakeApiTransport(401, "");
        var client = new ClaudeApiClient(transport);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123"));

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task FetchUsageDataAsync_TreatsCloudflareChallengeAs403Unauthorized()
    {
        var transport = new FakeApiTransport(403, "<!DOCTYPE html><html><head><title>Just a moment...</title>");
        var client = new ClaudeApiClient(transport);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123"));

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task FetchUsageDataAsync_ThrowsNonUnauthorizedOn500()
    {
        var transport = new FakeApiTransport(500, "server error");
        var client = new ClaudeApiClient(transport);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123"));

        Assert.False(ex.IsUnauthorized);
    }

    [Fact]
    public async Task FetchOrganizationsAsync_ParsesOrganizationList()
    {
        const string json = """[{"uuid":"org-1","name":"Acme","capabilities":[]},{"uuid":"org-2","name":"Beta","capabilities":[]}]""";
        var transport = new FakeApiTransport(200, json);
        var client = new ClaudeApiClient(transport);

        var organizations = await client.FetchOrganizationsAsync("sk-ant-sid01-test-key");

        Assert.Equal(2, organizations.Count);
        Assert.Equal("org-1", organizations[0].Uuid);
        Assert.Equal("Acme", organizations[0].Name);
    }

    [Fact]
    public async Task FetchOrganizationsAsync_ThrowsWhenEmpty()
    {
        var transport = new FakeApiTransport(200, "[]");
        var client = new ClaudeApiClient(transport);

        await Assert.ThrowsAsync<ClaudeApiException>(() => client.FetchOrganizationsAsync("sk-ant-sid01-test-key"));
    }

    [Fact]
    public async Task FetchUsageDataViaCliOAuthAsync_ParsesRateLimitHeaders()
    {
        var sessionReset = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, new Dictionary<string, string>
        {
            ["anthropic-ratelimit-unified-5h-utilization"] = "0.42",
            ["anthropic-ratelimit-unified-5h-reset"] = sessionReset.ToString(),
            ["anthropic-ratelimit-unified-7d-utilization"] = "0.185",
            ["anthropic-ratelimit-unified-7d-reset"] = weeklyReset.ToString()
        });
        var client = new ClaudeApiClient(new FakeApiTransport(200, ""), new HttpClient(handler));

        var usage = await client.FetchUsageDataViaCliOAuthAsync("test-access-token");

        Assert.Equal(42, usage.SessionPercentage);
        Assert.Equal(18.5, usage.WeeklyPercentage, precision: 5);
        Assert.Equal(0, usage.OpusWeeklyPercentage);
        Assert.Equal(0, usage.SonnetWeeklyPercentage);
        Assert.Null(usage.SonnetWeeklyResetTime);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(sessionReset), usage.SessionResetTime);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(weeklyReset), usage.WeeklyResetTime);
    }

    [Fact]
    public async Task FetchUsageDataViaCliOAuthAsync_SendsBearerTokenAndOAuthHeaders()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, new Dictionary<string, string>());
        var client = new ClaudeApiClient(new FakeApiTransport(200, ""), new HttpClient(handler));

        await client.FetchUsageDataViaCliOAuthAsync("test-access-token");

        Assert.Equal("https://api.anthropic.com/v1/messages", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer test-access-token", handler.LastRequest.Headers.GetValues("Authorization").Single());
        Assert.Equal("oauth-2025-04-20", handler.LastRequest.Headers.GetValues("anthropic-beta").Single());
    }

    [Fact]
    public async Task FetchUsageDataViaCliOAuthAsync_DefaultsMissingHeadersToZeroAndFallbackResets()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, new Dictionary<string, string>());
        var client = new ClaudeApiClient(new FakeApiTransport(200, ""), new HttpClient(handler));

        var usage = await client.FetchUsageDataViaCliOAuthAsync("test-access-token");

        Assert.Equal(0, usage.SessionPercentage);
        Assert.Equal(0, usage.WeeklyPercentage);
        Assert.True(usage.SessionResetTime > DateTimeOffset.Now);
        Assert.True(usage.WeeklyResetTime > DateTimeOffset.Now);
    }

    [Fact]
    public async Task FetchUsageDataViaCliOAuthAsync_ThrowsUnauthorizedOn401()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized);
        var client = new ClaudeApiClient(new FakeApiTransport(200, ""), new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataViaCliOAuthAsync("test-access-token"));

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task FetchUsageDataViaCliOAuthAsync_ThrowsNonUnauthorizedOn500()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var client = new ClaudeApiClient(new FakeApiTransport(200, ""), new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataViaCliOAuthAsync("test-access-token"));

        Assert.False(ex.IsUnauthorized);
    }
}
