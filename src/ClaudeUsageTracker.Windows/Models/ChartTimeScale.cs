namespace ClaudeUsageTracker.Windows.Models;

/// <summary>Time-window options for the usage history chart, ported from macOS's ChartTimeScale.</summary>
public enum ChartTimeScale
{
    Hours5 = 5,
    Hours24 = 24,
    Days7 = 168,
    Days30 = 720
}

public static class ChartTimeScaleExtensions
{
    public static string Label(this ChartTimeScale scale) => scale switch
    {
        ChartTimeScale.Hours5 => "5h",
        ChartTimeScale.Hours24 => "24h",
        ChartTimeScale.Days7 => "7d",
        ChartTimeScale.Days30 => "30d",
        _ => scale.ToString()
    };
}
