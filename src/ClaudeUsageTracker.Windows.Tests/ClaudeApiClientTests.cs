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
}
