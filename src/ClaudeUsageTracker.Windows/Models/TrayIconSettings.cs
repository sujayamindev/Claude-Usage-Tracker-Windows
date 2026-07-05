namespace ClaudeUsageTracker.Windows.Models;

/// <summary>
/// User-configurable tray icon appearance settings, persisted by TrayIconSettingsStore.
/// </summary>
public sealed class TrayIconSettings
{
    public TrayIconStyle Style { get; set; } = TrayIconStyle.ProgressRing;

    public static TrayIconSettings CreateDefault() => new();
}
