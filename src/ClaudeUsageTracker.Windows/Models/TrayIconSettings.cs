namespace ClaudeUsageTracker.Windows.Models;

public sealed class TrayIconSettings
{
    public TrayIconStyle Style { get; set; } = TrayIconStyle.ProgressRing;

    public TrayIconColorMode ColorMode { get; set; } = TrayIconColorMode.MultiColor;

    /// <summary>Hex color string used when ColorMode is SingleColor (e.g. "#00BFFF").</summary>
    public string SingleColorHex { get; set; } = "#00BFFF";

    /// <summary>When true, a time-elapsed tick colored by 6-tier pace status is drawn on the icon.</summary>
    public bool ShowPaceMarker { get; set; } = false;

    /// <summary>When true, popover labels show remaining capacity instead of used percentage.</summary>
    public bool ShowRemainingPercentage { get; set; } = false;

    public static TrayIconSettings CreateDefault() => new();
}
