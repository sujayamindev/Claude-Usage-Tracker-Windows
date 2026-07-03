using System.Text.Json;

namespace ClaudeUsageTracker.Windows.Models;

/// <summary>
/// Parsed subset of Claude Code CLI's statusline stdin JSON needed for the coding-context half
/// of the statusline (current directory, active model, context-window usage). Fields are parsed
/// defensively: a missing/renamed field just nulls out that piece rather than failing the whole
/// parse, since Claude Code's own JSON schema for this hook is not guaranteed stable across
/// versions (see docs/superpowers/specs/2026-07-03-statusline-installer-design.md, Open Risks).
/// </summary>
public sealed record StatuslineInput(string? CurrentDirectory, string? ModelDisplayName, double? ContextWindowPercentage)
{
    public static StatuslineInput? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;

            string? currentDirectory = root.TryGetProperty("cwd", out var cwdProp) &&
                cwdProp.ValueKind == JsonValueKind.String
                ? cwdProp.GetString()
                : null;

            string? modelDisplayName = null;
            if (root.TryGetProperty("model", out var modelProp) &&
                modelProp.ValueKind == JsonValueKind.Object &&
                modelProp.TryGetProperty("display_name", out var displayNameProp) &&
                displayNameProp.ValueKind == JsonValueKind.String)
            {
                modelDisplayName = displayNameProp.GetString();
            }

            double? contextWindowPercentage = null;
            if (root.TryGetProperty("context_window_percentage", out var contextProp) &&
                contextProp.ValueKind == JsonValueKind.Number &&
                contextProp.TryGetDouble(out var contextValue))
            {
                contextWindowPercentage = contextValue;
            }

            return new StatuslineInput(currentDirectory, modelDisplayName, contextWindowPercentage);
        }
    }
}
