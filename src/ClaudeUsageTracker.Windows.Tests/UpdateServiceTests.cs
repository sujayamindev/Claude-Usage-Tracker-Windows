using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("0.1.1", "v0.1.2", true)]
    [InlineData("0.1.1", "v0.2.0", true)]
    [InlineData("0.1.1", "v1.0.0", true)]
    [InlineData("0.1.1", "0.1.2", true)]
    [InlineData("0.1.1", "v0.1.1", false)]
    [InlineData("0.1.1", "v0.1.0", false)]
    [InlineData("0.1.1", "v0.0.9", false)]
    public void IsNewerVersion_ComparesCorrectly(string currentVersion, string latestTag, bool expected)
    {
        var current = Version.Parse(currentVersion);

        var result = UpdateService.IsNewerVersion(current, latestTag);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("v1.2.3-beta")]
    [InlineData("")]
    public void IsNewerVersion_TreatsMalformedTagsAsNotNewer(string latestTag)
    {
        var current = Version.Parse("0.1.1");

        Assert.False(UpdateService.IsNewerVersion(current, latestTag));
    }
}
