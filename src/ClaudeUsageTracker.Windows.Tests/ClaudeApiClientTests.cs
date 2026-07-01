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
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, UsageResponseJson);
        var client = new ClaudeApiClient(new HttpClient(handler));

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
    public async Task FetchUsageDataAsync_DefaultsMissingFieldsToZero()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = new ClaudeApiClient(new HttpClient(handler));

        var usage = await client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123");

        Assert.Equal(0, usage.SessionPercentage);
        Assert.Equal(0, usage.WeeklyPercentage);
        Assert.Null(usage.SonnetWeeklyResetTime);
    }

    [Fact]
    public async Task FetchUsageDataAsync_SendsSessionKeyAsCookie()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, UsageResponseJson);
        var client = new ClaudeApiClient(new HttpClient(handler));

        await client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123");

        var cookie = Assert.Single(handler.LastRequest!.Headers.GetValues("Cookie"));
        Assert.Equal("sessionKey=sk-ant-sid01-test-key", cookie);
    }

    [Fact]
    public async Task FetchUsageDataAsync_ThrowsUnauthorizedOn401()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "");
        var client = new ClaudeApiClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123"));

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task FetchUsageDataAsync_ThrowsNonUnauthorizedOn500()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "server error");
        var client = new ClaudeApiClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.FetchUsageDataAsync("sk-ant-sid01-test-key", "org-123"));

        Assert.False(ex.IsUnauthorized);
    }

    [Fact]
    public async Task FetchOrganizationsAsync_ParsesOrganizationList()
    {
        const string json = """[{"uuid":"org-1","name":"Acme","capabilities":[]},{"uuid":"org-2","name":"Beta","capabilities":[]}]""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var client = new ClaudeApiClient(new HttpClient(handler));

        var organizations = await client.FetchOrganizationsAsync("sk-ant-sid01-test-key");

        Assert.Equal(2, organizations.Count);
        Assert.Equal("org-1", organizations[0].Uuid);
        Assert.Equal("Acme", organizations[0].Name);
    }

    [Fact]
    public async Task FetchOrganizationsAsync_ThrowsWhenEmpty()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = new ClaudeApiClient(new HttpClient(handler));

        await Assert.ThrowsAsync<ClaudeApiException>(() => client.FetchOrganizationsAsync("sk-ant-sid01-test-key"));
    }
}
