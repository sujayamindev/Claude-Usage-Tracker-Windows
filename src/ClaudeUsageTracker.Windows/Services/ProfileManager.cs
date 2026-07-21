using System.Threading;
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
    private readonly UsageHistoryService _usageHistoryService;
    private readonly List<Profile> _profiles;
    private Guid _activeProfileId;

    public IReadOnlyList<Profile> Profiles => _profiles;
    public Profile ActiveProfile => _profiles.First(p => p.Id == _activeProfileId);

    public event EventHandler<Profile>? ActiveProfileChanged;
    public event EventHandler? ProfilesChanged;

    public ProfileManager(ProfileStore store, CliCredentialReader cliCredentialReader, UsageHistoryService usageHistoryService)
    {
        _store = store;
        _cliCredentialReader = cliCredentialReader;
        _usageHistoryService = usageHistoryService;

        var data = _store.Load();
        if (data is not null)
        {
            _profiles = data.Profiles;
            _activeProfileId = data.ActiveProfileId;

            // Guard against a hand-edited or partially-written profiles.json: structurally valid
            // JSON can still be semantically inconsistent (ActiveProfileId matching no profile, or
            // an empty profile list), which every in-app mutator otherwise prevents.
            if (_profiles.Count == 0)
                throw new ProfileStoreException("profiles.json contains no profiles.", null);

            if (_profiles.All(p => p.Id != _activeProfileId))
            {
                _activeProfileId = _profiles[0].Id;
                Persist();
            }
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
        else if (TryReadCliCredentialsWithRetry() is { IsExpired: false })
        {
            profile.AuthMode = ProfileAuthMode.CliOAuth;
        }

        return profile;
    }

    // Synchronous duplicate of CliCredentialReader.TryReadWithRetryAsync's retry logic: the
    // constructor must stay synchronous to avoid a WPF Dispatcher deadlock (awaiting Task.Delay
    // would capture the calling SynchronizationContext), and this only ever runs once during
    // first-run/upgrade migration, so the ~1s worst-case blocking cost is acceptable exactly here
    // (unlike on every regular poll tick).
    private CliCredentials? TryReadCliCredentialsWithRetry(int maxAttempts = 3, TimeSpan? delayBetweenAttempts = null)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromMilliseconds(500);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_cliCredentialReader.TryRead() is { IsExpired: false } credentials)
                return credentials;

            if (attempt < maxAttempts - 1)
                Thread.Sleep(delay);
        }
        return null;
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
        _usageHistoryService.DeleteHistory(id);
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
