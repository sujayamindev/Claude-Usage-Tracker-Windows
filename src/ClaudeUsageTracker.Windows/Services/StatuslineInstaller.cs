using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;

namespace ClaudeUsageTracker.Windows.Services;

public sealed class StatuslineSettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Merges/removes the "statusLine" key in ~/.claude/settings.json, pointing Claude Code CLI at
/// this exe's own --statusline mode instead of a separate script file. Preserves all other keys
/// (permissions, hooks, etc.) already present in the file. Fails closed on a corrupt existing
/// file rather than risking clobbering the user's other settings.
/// </summary>
public sealed class StatuslineInstaller(string? settingsFilePath = null, string? exePath = null)
{
    private readonly string _settingsFilePath = settingsFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    private readonly string _exePath = exePath ?? Environment.ProcessPath ??
        throw new InvalidOperationException("Could not determine the running executable path.");

    private string ExpectedCommand => $"\"{_exePath}\" --statusline";

    public bool IsEnabled() =>
        LoadSettings()?["statusLine"]?["command"]?.GetValue<string>() == ExpectedCommand;

    public void Enable()
    {
        var root = LoadSettings() ?? new JsonObject();

        root["statusLine"] = new JsonObject
        {
            ["type"] = "command",
            ["command"] = ExpectedCommand
        };

        SaveSettings(root);
    }

    public void Disable()
    {
        var root = LoadSettings();
        if (root is null)
            return;

        root.Remove("statusLine");
        SaveSettings(root);
    }

    private JsonObject? LoadSettings()
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
            throw new StatuslineSettingsException($"Could not read {_settingsFilePath}: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            throw new StatuslineSettingsException(
                $"{_settingsFilePath} is not valid JSON — fix or remove it before changing the statusline setting.", ex);
        }
    }

    private void SaveSettings(JsonObject root)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(_settingsFilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException ex)
        {
            throw new StatuslineSettingsException($"Could not write {_settingsFilePath}: {ex.Message}", ex);
        }
    }
}
