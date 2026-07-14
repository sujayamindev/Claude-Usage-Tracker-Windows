using System.IO;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public sealed class ProfileStoreException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class ProfileData
{
    public List<Profile> Profiles { get; set; } = [];
    public Guid ActiveProfileId { get; set; }
}

/// <summary>
/// Loads/saves non-secret profile data as JSON under %LOCALAPPDATA%\ClaudeUsageTracker\, same
/// pattern as TrayIconSettingsStore/NotificationSettingsStore. Unlike those, Load() returns null
/// (not a default instance) when the file doesn't exist yet — ProfileManager uses that null to
/// know a one-time migration from the pre-multi-profile single credential is needed.
/// </summary>
public sealed class ProfileStore(string? settingsFilePath = null)
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath = settingsFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeUsageTracker", "profiles.json");

    public ProfileData? Load()
    {
        if (!File.Exists(_settingsFilePath))
            return null;

        string json;
        try
        {
            json = File.ReadAllText(_settingsFilePath);
        }
        catch (IOException ex)
        {
            throw new ProfileStoreException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ProfileStoreException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<ProfileData>(json)
                ?? throw new ProfileStoreException($"{_settingsFilePath} deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new ProfileStoreException(
                $"{_settingsFilePath} is not valid JSON — fix or remove it to reset profiles.", ex);
        }
    }

    public void Save(ProfileData data)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(data, _writeOptions));
        }
        catch (IOException ex)
        {
            throw new ProfileStoreException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ProfileStoreException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
    }
}
