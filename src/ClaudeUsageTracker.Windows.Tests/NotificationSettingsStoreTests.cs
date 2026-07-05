using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class NotificationSettingsStoreTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"notification-settings-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var store = new NotificationSettingsStore(TempSettingsPath());

        var settings = store.Load();

        Assert.True(settings.NotificationsEnabled);
        Assert.True(settings.SoundEnabled);
        Assert.Equal(6, settings.Thresholds.Count);
        Assert.Contains(settings.Thresholds, t => t.Metric == NotificationMetric.Session && t.Percentage == 75);
        Assert.Contains(settings.Thresholds, t => t.Metric == NotificationMetric.Weekly && t.Percentage == 95);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSettings()
    {
        var path = TempSettingsPath();
        try
        {
            var store = new NotificationSettingsStore(path);
            var settings = NotificationSettings.CreateDefault();
            settings.NotificationsEnabled = false;
            settings.SoundEnabled = false;
            settings.Thresholds.Add(new NotificationThreshold { Metric = NotificationMetric.Session, Percentage = 50, Enabled = false });

            store.Save(settings);
            var reloaded = store.Load();

            Assert.False(reloaded.NotificationsEnabled);
            Assert.False(reloaded.SoundEnabled);
            Assert.Equal(7, reloaded.Thresholds.Count);
            Assert.Contains(reloaded.Thresholds, t => t.Metric == NotificationMetric.Session && t.Percentage == 50 && !t.Enabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_CreatesParentDirectoryWhenMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"notification-settings-dir-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "notification-settings.json");
        try
        {
            new NotificationSettingsStore(path).Save(NotificationSettings.CreateDefault());

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ThrowsOnCorruptExistingFile()
    {
        var path = TempSettingsPath();
        File.WriteAllText(path, "{ not valid json");
        try
        {
            Assert.Throws<NotificationSettingsException>(() => new NotificationSettingsStore(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
