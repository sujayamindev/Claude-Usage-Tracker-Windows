namespace ClaudeUsageTracker.Windows.Models;

public enum NotificationMetric
{
    Session,
    Weekly
}

public sealed class NotificationThreshold
{
    public NotificationMetric Metric { get; set; }
    public int Percentage { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// User-configurable threshold-alert settings, persisted by NotificationSettingsStore.
/// Defaults mirror the macOS app's 75/90/95% alert thresholds, applied independently to
/// both the session and weekly percentages.
/// </summary>
public sealed class NotificationSettings
{
    public bool NotificationsEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public List<NotificationThreshold> Thresholds { get; set; } = [];

    public static NotificationSettings CreateDefault()
    {
        List<NotificationThreshold> thresholds = [];
        foreach (var metric in new[] { NotificationMetric.Session, NotificationMetric.Weekly })
        {
            foreach (var percentage in new[] { 75, 90, 95 })
                thresholds.Add(new NotificationThreshold { Metric = metric, Percentage = percentage, Enabled = true });
        }

        return new NotificationSettings { Thresholds = thresholds };
    }
}
