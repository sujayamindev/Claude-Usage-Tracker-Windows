using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class ProfileStoreTests
{
    private static string TempProfilesPath() =>
        Path.Combine(Path.GetTempPath(), $"profiles-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_ReturnsNullWhenFileDoesNotExist()
    {
        var store = new ProfileStore(TempProfilesPath());

        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProfileData()
    {
        var path = TempProfilesPath();
        try
        {
            var store = new ProfileStore(path);
            var profile = new Profile { Name = "Work", AuthMode = ProfileAuthMode.SessionKey, OrganizationId = "org-1", OrganizationName = "Acme" };
            var data = new ProfileData { Profiles = [profile], ActiveProfileId = profile.Id };

            store.Save(data);
            var reloaded = store.Load();

            Assert.NotNull(reloaded);
            Assert.Equal(profile.Id, reloaded!.ActiveProfileId);
            Assert.Single(reloaded.Profiles);
            Assert.Equal("Work", reloaded.Profiles[0].Name);
            Assert.Equal("Acme", reloaded.Profiles[0].OrganizationName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_CreatesParentDirectoryWhenMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"profiles-dir-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "profiles.json");
        try
        {
            new ProfileStore(path).Save(new ProfileData { Profiles = [], ActiveProfileId = Guid.Empty });

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ThrowsOnCorruptExistingFile()
    {
        var path = TempProfilesPath();
        File.WriteAllText(path, "{ not valid json");
        try
        {
            Assert.Throws<ProfileStoreException>(() => new ProfileStore(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
