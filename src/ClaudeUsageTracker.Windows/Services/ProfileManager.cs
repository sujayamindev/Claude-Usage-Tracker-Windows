using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public enum ProfileErrorReason
{
    CannotDeleteLastProfile,
    ProfileNotFound
}

public sealed class ProfileException(ProfileErrorReason reason) : Exception(reason switch
{
    ProfileErrorReason.CannotDeleteLastProfile => "Can't delete the last remaining profile.",
    ProfileErrorReason.ProfileNotFound => "Profile not found.",
    _ => "Unknown profile error."
})
{
    public ProfileErrorReason Reason { get; } = reason;
}

/// <summary>
/// Coordinates profile create/rename/delete/switch and owns the one-time migration from the
/// pre-multi-profile single credential. Every mutating method persists to ProfileStore before
/// firing its event, so a crash mid-update never leaves in-memory state ahead of disk.
/// </summary>
public sealed class ProfileManager
{
    private readonly ProfileStore _store;
    private readonly CliCredentialReader _cliCredentialReader;
    private readonly List<Profile> _profiles;
    private Guid _activeProfileId;

    public IReadOnlyList<Profile> Profiles => _profiles;
    public Profile ActiveProfile => _profiles.First(p => p.Id == _activeProfileId);

    public event EventHandler<Profile>? ActiveProfileChanged;
    public event EventHandler? ProfilesChanged;

    public ProfileManager(ProfileStore store, CliCredentialReader cliCredentialReader)
    {
        _store = store;
        _cliCredentialReader = cliCredentialReader;

        var data = _store.Load();
        if (data is not null)
        {
            _profiles = data.Profiles;
            _activeProfileId = data.ActiveProfileId;
        }
        else
        {
            var migrated = MigrateFromLegacy();
            _profiles = [migrated];
            _activeProfileId = migrated.Id;
            Persist();
        }
    }

    private Profile MigrateFromLegacy()
    {
        var profile = new Profile { Name = "Default" };

        if (CredentialStore.TryLoadLegacy(out var legacyCredentials) && legacyCredentials is not null)
        {
            profile.AuthMode = ProfileAuthMode.SessionKey;
            profile.OrganizationId = legacyCredentials.OrganizationId;
            profile.OrganizationName = legacyCredentials.OrganizationName;
            CredentialStore.Save(profile.Id, legacyCredentials);
            CredentialStore.ClearLegacy();
        }
        else if (_cliCredentialReader.TryRead() is { IsExpired: false })
        {
            profile.AuthMode = ProfileAuthMode.CliOAuth;
        }

        return profile;
    }

    public Profile CreateProfile(string name, ProfileAuthMode authMode, StoredCredentials? credentials)
    {
        var profile = new Profile { Name = name, AuthMode = authMode };
        if (credentials is not null)
        {
            profile.OrganizationId = credentials.OrganizationId;
            profile.OrganizationName = credentials.OrganizationName;
            CredentialStore.Save(profile.Id, credentials);
        }

        _profiles.Add(profile);
        _activeProfileId = profile.Id;
        Persist();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        ActiveProfileChanged?.Invoke(this, profile);
        return profile;
    }

    public void RenameProfile(Guid id, string newName)
    {
        var profile = FindOrThrow(id);
        profile.Name = newName;
        Persist();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteProfile(Guid id)
    {
        if (_profiles.Count == 1)
            throw new ProfileException(ProfileErrorReason.CannotDeleteLastProfile);

        var profile = FindOrThrow(id);
        CredentialStore.Clear(id);
        _profiles.Remove(profile);

        var wasActive = _activeProfileId == id;
        if (wasActive)
            _activeProfileId = _profiles[0].Id;

        Persist();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        if (wasActive)
            ActiveProfileChanged?.Invoke(this, ActiveProfile);
    }

    public void SwitchTo(Guid id)
    {
        if (id == _activeProfileId)
            return;

        var profile = FindOrThrow(id);
        _activeProfileId = id;
        profile.LastUsedAt = DateTimeOffset.UtcNow;
        Persist();
        ActiveProfileChanged?.Invoke(this, profile);
    }

    /// <summary>
    /// Persists a (re-)authentication result for the active profile. Needed because SetupWindow
    /// no longer persists credentials itself (Step 2) — App.xaml.cs calls this after a first-run
    /// or re-auth SetupWindow dialog succeeds for the currently active profile.
    /// </summary>
    public void UpdateActiveProfileCredentials(ProfileAuthMode authMode, StoredCredentials? credentials)
    {
        var profile = ActiveProfile;
        profile.AuthMode = authMode;
        if (credentials is not null)
        {
            profile.OrganizationId = credentials.OrganizationId;
            profile.OrganizationName = credentials.OrganizationName;
            CredentialStore.Save(profile.Id, credentials);
        }
        Persist();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private Profile FindOrThrow(Guid id) =>
        _profiles.FirstOrDefault(p => p.Id == id) ?? throw new ProfileException(ProfileErrorReason.ProfileNotFound);

    private void Persist() => _store.Save(new ProfileData { Profiles = _profiles, ActiveProfileId = _activeProfileId });
}
