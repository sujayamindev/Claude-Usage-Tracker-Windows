using System.Linq;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class ThresholdNotifierTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"threshold-notifier-test-{Guid.NewGuid():N}.json");

    private static ClaudeUsage UsageWith(double sessionPercentage, double weeklyPercentage, DateTimeOffset? sessionReset = null, DateTimeOffset? weeklyReset = null) => new()
    {
        SessionPercentage = sessionPercentage,
        SessionResetTime = sessionReset ?? DateTimeOffset.Now.AddHours(5),
        WeeklyPercentage = weeklyPercentage,
        WeeklyResetTime = weeklyReset ?? DateTimeOffset.Now.AddDays(7),
        OpusWeeklyPercentage = 0,
        SonnetWeeklyPercentage = 0,
        LastUpdated = DateTimeOffset.Now
    };

    [Fact]
    public void Evaluate_FiresForEachCrossedThresholdOnFirstPoll()
    {
        var notifier = new ThresholdNotifier(new NotificationSettingsStore(TempSettingsPath()));

        var events = notifier.Evaluate(UsageWith(sessionPercentage: 92, weeklyPercentage: 10));

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.Metric == NotificationMetric.Session && e.Percentage == 75);
        Assert.Contains(events, e => e.Metric == NotificationMetric.Session && e.Percentage == 90);
    }

    [Fact]
    public void Evaluate_DoesNotRefireAlreadyCrossedThresholdOnSubsequentPolls()
    {
        var notifier = new ThresholdNotifier(new NotificationSettingsStore(TempSettingsPath()));
        notifier.Evaluate(UsageWith(sessionPercentage: 80, weeklyPercentage: 0));

        var events = notifier.Evaluate(UsageWith(sessionPercentage: 85, weeklyPercentage: 0));

        Assert.Empty(events);
    }

    [Fact]
    public void Evaluate_FiresNewlyCrossedThresholdOnLaterPoll()
    {
        var notifier = new ThresholdNotifier(new NotificationSettingsStore(TempSettingsPath()));
        notifier.Evaluate(UsageWith(sessionPercentage: 80, weeklyPercentage: 0));

        var events = notifier.Evaluate(UsageWith(sessionPercentage: 91, weeklyPercentage: 0));

        var single = Assert.Single(events);
        Assert.Equal(NotificationMetric.Session, single.Metric);
        Assert.Equal(90, single.Percentage);
    }

    [Fact]
    public void Evaluate_RearmsAfterSessionResetTimeAdvances()
    {
        var notifier = new ThresholdNotifier(new NotificationSettingsStore(TempSettingsPath()));
        var firstReset = DateTimeOffset.Now.AddHours(5);
        notifier.Evaluate(UsageWith(sessionPercentage: 80, weeklyPercentage: 0, sessionReset: firstReset));

        var events = notifier.Evaluate(UsageWith(sessionPercentage: 80, weeklyPercentage: 0, sessionReset: firstReset.AddHours(5)));

        var single = Assert.Single(events);
        Assert.Equal(NotificationMetric.Session, single.Metric);
        Assert.Equal(75, single.Percentage);
    }

    [Fact]
    public void Evaluate_SkipsDisabledThresholds()
    {
        var path = TempSettingsPath();
        var store = new NotificationSettingsStore(path);
        var settings = NotificationSettings.CreateDefault();
        settings.Thresholds.Single(t => t.Metric == NotificationMetric.Session && t.Percentage == 75).Enabled = false;
        store.Save(settings);

        var events = new ThresholdNotifier(store).Evaluate(UsageWith(sessionPercentage: 80, weeklyPercentage: 0));

        Assert.DoesNotContain(events, e => e.Metric == NotificationMetric.Session && e.Percentage == 75);
    }

    [Fact]
    public void Evaluate_ReturnsEmptyWhenNotificationsDisabled()
    {
        var path = TempSettingsPath();
        var store = new NotificationSettingsStore(path);
        var settings = NotificationSettings.CreateDefault();
        settings.NotificationsEnabled = false;
        store.Save(settings);

        var events = new ThresholdNotifier(store).Evaluate(UsageWith(sessionPercentage: 99, weeklyPercentage: 99));

        Assert.Empty(events);
    }
}
