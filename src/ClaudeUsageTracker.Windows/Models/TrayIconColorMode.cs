namespace ClaudeUsageTracker.Windows.Models;

public enum TrayIconColorMode
{
    MultiColor,   // green/orange/red by UsageStatusLevel — default
    Monochrome,   // white — Windows tray is always dark-background
    SingleColor   // user-chosen hex color stored in TrayIconSettings.SingleColorHex
}
