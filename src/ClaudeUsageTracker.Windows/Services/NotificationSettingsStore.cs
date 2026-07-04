using System.IO;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public sealed class NotificationSettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Loads/saves threshold-notification settings as JSON. Unlike StatuslineCache/StatuslineInstaller
/// (which live under ~/.claude because they're Claude Code CLI integration surfaces), this settings
/// file is purely internal to the app, so it gets its own folder under %LOCALAPPDATA%.
/// </summary>
public sealed class NotificationSettingsStore(string? settingsFilePath = null)
{
    private readonly string _settingsFilePath = settingsFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeUsageTracker", "notification-settings.json");

    public NotificationSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
            return NotificationSettings.CreateDefault();

        string json;
        try
        {
            json = File.ReadAllText(_settingsFilePath);
        }
        catch (IOException ex)
        {
            throw new NotificationSettingsException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new NotificationSettingsException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(json))
            return NotificationSettings.CreateDefault();

        try
        {
            return JsonSerializer.Deserialize<NotificationSettings>(json) ?? NotificationSettings.CreateDefault();
        }
        catch (JsonException ex)
        {
            throw new NotificationSettingsException(
                $"{_settingsFilePath} is not valid JSON — fix or remove it before changing notification settings.", ex);
        }
    }

    public void Save(NotificationSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException ex)
        {
            throw new NotificationSettingsException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new NotificationSettingsException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
    }
}
