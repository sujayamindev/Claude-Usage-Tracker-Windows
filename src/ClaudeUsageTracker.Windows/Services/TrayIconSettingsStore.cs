using System.IO;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public sealed class TrayIconSettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Loads/saves tray icon appearance settings as JSON under %LOCALAPPDATA%\ClaudeUsageTracker\.
/// </summary>
public sealed class TrayIconSettingsStore(string? settingsFilePath = null)
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath = settingsFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeUsageTracker", "tray-icon-settings.json");

    public TrayIconSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
            return TrayIconSettings.CreateDefault();

        string json;
        try
        {
            json = File.ReadAllText(_settingsFilePath);
        }
        catch (IOException ex)
        {
            throw new TrayIconSettingsException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new TrayIconSettingsException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<TrayIconSettings>(json) ?? TrayIconSettings.CreateDefault();
        }
        catch (JsonException ex)
        {
            throw new TrayIconSettingsException(
                $"{_settingsFilePath} is not valid JSON — fix or remove it to reset icon style settings.", ex);
        }
    }

    public void Save(TrayIconSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, _writeOptions));
        }
        catch (IOException ex)
        {
            throw new TrayIconSettingsException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new TrayIconSettingsException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
    }
}
