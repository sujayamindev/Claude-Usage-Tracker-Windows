using System.Text.Json;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class CliCredentialReaderTests
{
    private static string WriteTempCredentialsFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"claude-credentials-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void TryRead_ParsesValidNonExpiredCredentials()
    {
        var expiresAtMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        var json = $$"""
        {
            "claudeAiOauth": {
                "accessToken": "test-access-token",
                "refreshToken": "test-refresh-token",
                "expiresAt": {{expiresAtMs}},
                "subscriptionType": "pro",
                "scopes": ["user:inference"]
            }
        }
        """;
        var path = WriteTempCredentialsFile(json);

        try
        {
            var reader = new CliCredentialReader(path);
            var credentials = reader.TryRead();

            Assert.NotNull(credentials);
            Assert.Equal("test-access-token", credentials!.AccessToken);
            Assert.Equal("pro", credentials.SubscriptionType);
            Assert.False(credentials.IsExpired);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_MarksExpiredWhenExpiresAtMsIsInThePast()
    {
        var expiresAtMs = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        var json = $$"""
        {
            "claudeAiOauth": {
                "accessToken": "test-access-token",
                "expiresAt": {{expiresAtMs}}
            }
        }
        """;
        var path = WriteTempCredentialsFile(json);

        try
        {
            var credentials = new CliCredentialReader(path).TryRead();

            Assert.NotNull(credentials);
            Assert.True(credentials!.IsExpired);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_TreatsSecondsEpochCorrectly()
    {
        // expiresAt values below 1e12 are seconds, not milliseconds.
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var json = $$"""
        {
            "claudeAiOauth": {
                "accessToken": "test-access-token",
                "expiresAt": {{expiresAtSeconds}}
            }
        }
        """;
        var path = WriteTempCredentialsFile(json);

        try
        {
            var credentials = new CliCredentialReader(path).TryRead();

            Assert.NotNull(credentials);
            Assert.False(credentials!.IsExpired);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_ReturnsNullWhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");

        var credentials = new CliCredentialReader(path).TryRead();

        Assert.Null(credentials);
    }

    [Fact]
    public void TryRead_ReturnsNullOnMalformedJson()
    {
        var path = WriteTempCredentialsFile("{ this is not valid json");

        try
        {
            var credentials = new CliCredentialReader(path).TryRead();
            Assert.Null(credentials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_ReturnsNullWhenClaudeAiOauthKeyIsMissing()
    {
        var path = WriteTempCredentialsFile("""{ "someOtherKey": {} }""");

        try
        {
            var credentials = new CliCredentialReader(path).TryRead();
            Assert.Null(credentials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_ReturnsNullWhenAccessTokenIsMissing()
    {
        var path = WriteTempCredentialsFile("""{ "claudeAiOauth": { "expiresAt": 1 } }""");

        try
        {
            var credentials = new CliCredentialReader(path).TryRead();
            Assert.Null(credentials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TryReadWithRetryAsync_ReturnsImmediatelyOnFirstSuccess()
    {
        var expiresAtMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        var json = $$"""{ "claudeAiOauth": { "accessToken": "test-token", "expiresAt": {{expiresAtMs}} } }""";
        var path = WriteTempCredentialsFile(json);

        try
        {
            var reader = new CliCredentialReader(path);
            var credentials = await reader.TryReadWithRetryAsync(maxAttempts: 3, delayBetweenAttempts: TimeSpan.Zero);

            Assert.NotNull(credentials);
            Assert.Equal("test-token", credentials!.AccessToken);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TryReadWithRetryAsync_RecoversWhenFileBecomesValidBeforeLastAttempt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"claude-credentials-test-{Guid.NewGuid():N}.json");
        // File does not exist yet — first attempt(s) must fail, then it appears.
        var reader = new CliCredentialReader(path);

        try
        {
            var readTask = Task.Run(async () =>
            {
                return await reader.TryReadWithRetryAsync(maxAttempts: 5, delayBetweenAttempts: TimeSpan.FromMilliseconds(50));
            });

            // Give the retry loop a couple of failed attempts before the file appears.
            await Task.Delay(120);
            var expiresAtMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            File.WriteAllText(path, $$"""{ "claudeAiOauth": { "accessToken": "recovered-token", "expiresAt": {{expiresAtMs}} } }""");

            var credentials = await readTask;

            Assert.NotNull(credentials);
            Assert.Equal("recovered-token", credentials!.AccessToken);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task TryReadWithRetryAsync_ReturnsNullWhenAllAttemptsFail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        var reader = new CliCredentialReader(path);

        var credentials = await reader.TryReadWithRetryAsync(maxAttempts: 3, delayBetweenAttempts: TimeSpan.Zero);

        Assert.Null(credentials);
    }

    [Fact]
    public async Task TryReadWithRetryAsync_KeepsRetryingWhenCredentialsAreExpired()
    {
        var expiresAtMs = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        var json = $$"""{ "claudeAiOauth": { "accessToken": "expired-token", "expiresAt": {{expiresAtMs}} } }""";
        var path = WriteTempCredentialsFile(json);

        try
        {
            var reader = new CliCredentialReader(path);
            var credentials = await reader.TryReadWithRetryAsync(maxAttempts: 3, delayBetweenAttempts: TimeSpan.Zero);

            Assert.Null(credentials);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
