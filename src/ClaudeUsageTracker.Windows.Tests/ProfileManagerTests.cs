using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class ProfileManagerTests
{
    private static string TempProfilesPath() =>
        Path.Combine(Path.GetTempPath(), $"profiles-test-{Guid.NewGuid():N}.json");

    private static string MissingCliCredentialsPath() =>
        Path.Combine(Path.GetTempPath(), $"cli-credentials-test-{Guid.NewGuid():N}.json");

    private static string WriteValidCliCredentialsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-credentials-test-{Guid.NewGuid():N}.json");
        var expiresAtMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        File.WriteAllText(path, $$"""
        {
            "claudeAiOauth": {
                "accessToken": "test-access-token",
                "refreshToken": "test-refresh-token",
                "expiresAt": {{expiresAtMs}},
                "subscriptionType": "pro",
                "scopes": ["user:inference"]
            }
        }
        """);
        return path;
    }

    [Fact]
    public void Constructor_WithNoExistingFileAndNoCredentials_CreatesSingleEmptyDefaultProfile()
    {
        var profilesPath = TempProfilesPath();
        try
        {
            var manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(MissingCliCredentialsPath()));

            Assert.Single(manager.Profiles);
            Assert.Equal("Default", manager.ActiveProfile.Name);
            Assert.Equal(ProfileAuthMode.SessionKey, manager.ActiveProfile.AuthMode);
            Assert.True(File.Exists(profilesPath));
        }
        finally
        {
            File.Delete(profilesPath);
        }
    }

    [Fact]
    public void Constructor_WithLegacyCredential_MigratesIntoDefaultProfileAndClearsLegacyEntry()
    {
        var profilesPath = TempProfilesPath();
        var legacyCredentials = new StoredCredentials($"sk-ant-sid01-test-{Guid.NewGuid():N}", "org-123", "Acme Inc");
        CredentialStore.SaveLegacy(legacyCredentials);
        ProfileManager? manager = null;
        try
        {
            manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(MissingCliCredentialsPath()));

            Assert.Single(manager.Profiles);
            Assert.Equal(ProfileAuthMode.SessionKey, manager.ActiveProfile.AuthMode);
            Assert.Equal("Acme Inc", manager.ActiveProfile.OrganizationName);
            Assert.True(CredentialStore.TryLoad(manager.ActiveProfile.Id, out var migrated));
            Assert.Equal(legacyCredentials.SessionKey, migrated!.SessionKey);
            Assert.False(CredentialStore.TryLoadLegacy(out _));
        }
        finally
        {
            File.Delete(profilesPath);
            if (manager is not null)
                CredentialStore.Clear(manager.ActiveProfile.Id);
        }
    }

    [Fact]
    public void Constructor_WithNoLegacyCredentialButWorkingCliLogin_MigratesToCliOAuthProfile()
    {
        var profilesPath = TempProfilesPath();
        var cliPath = WriteValidCliCredentialsFile();
        try
        {
            var manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(cliPath));

            Assert.Single(manager.Profiles);
            Assert.Equal(ProfileAuthMode.CliOAuth, manager.ActiveProfile.AuthMode);
        }
        finally
        {
            File.Delete(profilesPath);
            File.Delete(cliPath);
        }
    }

    [Fact]
    public void CreateProfile_AddsProfileSavesCredentialsAndActivatesIt()
    {
        var profilesPath = TempProfilesPath();
        ProfileManager? manager = null;
        Profile? created = null;
        try
        {
            manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(MissingCliCredentialsPath()));
            var credentials = new StoredCredentials($"sk-ant-sid01-test-{Guid.NewGuid():N}", "org-9", "Second Org");

            created = manager.CreateProfile("Second", ProfileAuthMode.SessionKey, credentials);

            Assert.Equal(2, manager.Profiles.Count);
            Assert.Equal(created.Id, manager.ActiveProfile.Id);
            Assert.True(CredentialStore.TryLoad(created.Id, out var loaded));
            Assert.Equal(credentials.SessionKey, loaded!.SessionKey);
        }
        finally
        {
            File.Delete(profilesPath);
            if (created is not null)
                CredentialStore.Clear(created.Id);
        }
    }

    [Fact]
    public void DeleteProfile_ThrowsWhenOnlyOneProfileRemains()
    {
        var profilesPath = TempProfilesPath();
        try
        {
            var manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(MissingCliCredentialsPath()));

            var ex = Assert.Throws<ProfileException>(() => manager.DeleteProfile(manager.ActiveProfile.Id));
            Assert.Equal(ProfileErrorReason.CannotDeleteLastProfile, ex.Reason);
        }
        finally
        {
            File.Delete(profilesPath);
        }
    }

    [Fact]
    public void DeleteProfile_RemovesProfileClearsCredentialsAndActivatesRemaining()
    {
        var profilesPath = TempProfilesPath();
        ProfileManager? manager = null;
        try
        {
            manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(MissingCliCredentialsPath()));
            var defaultProfileId = manager.ActiveProfile.Id;
            var second = manager.CreateProfile("Second", ProfileAuthMode.CliOAuth, null);

            manager.DeleteProfile(second.Id);

            Assert.Single(manager.Profiles);
            Assert.Equal(defaultProfileId, manager.ActiveProfile.Id);
        }
        finally
        {
            File.Delete(profilesPath);
        }
    }

    [Fact]
    public void SwitchTo_ChangesActiveProfileAndRaisesEvent()
    {
        var profilesPath = TempProfilesPath();
        ProfileManager? manager = null;
        Profile? second = null;
        try
        {
            manager = new ProfileManager(new ProfileStore(profilesPath), new CliCredentialReader(MissingCliCredentialsPath()));
            var defaultProfileId = manager.ActiveProfile.Id;
            second = manager.CreateProfile("Second", ProfileAuthMode.CliOAuth, null);

            Profile? raised = null;
            manager.ActiveProfileChanged += (_, profile) => raised = profile;
            manager.SwitchTo(defaultProfileId);

            Assert.Equal(defaultProfileId, manager.ActiveProfile.Id);
            Assert.Equal(defaultProfileId, raised?.Id);
        }
        finally
        {
            File.Delete(profilesPath);
            if (second is not null)
                CredentialStore.Clear(second.Id);
        }
    }
}
