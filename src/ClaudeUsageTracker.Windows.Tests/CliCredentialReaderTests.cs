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
}
