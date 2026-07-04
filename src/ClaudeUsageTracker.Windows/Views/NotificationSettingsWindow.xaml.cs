using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class NotificationSettingsWindow : FluentWindow
{
    private readonly NotificationSettingsStore _settingsStore;
    private bool _isUpdatingProgrammatically;

    public NotificationSettingsWindow(NotificationSettingsStore settingsStore)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _settingsStore = settingsStore;
        LoadAndRender();
    }

    private void LoadAndRender()
    {
        try
        {
            _isUpdatingProgrammatically = true;
            ErrorText.Text = string.Empty;

            var settings = _settingsStore.Load();
            NotificationsEnabledToggle.IsChecked = settings.NotificationsEnabled;
            SoundEnabledToggle.IsChecked = settings.SoundEnabled;
            RenderThresholds(SessionThresholdsPanel, settings, NotificationMetric.Session);
            RenderThresholds(WeeklyThresholdsPanel, settings, NotificationMetric.Weekly);
        }
        catch (NotificationSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            _isUpdatingProgrammatically = false;
        }
    }

    private void RenderThresholds(StackPanel panel, NotificationSettings settings, NotificationMetric metric)
    {
        panel.Children.Clear();

        foreach (var threshold in settings.Thresholds.Where(t => t.Metric == metric).OrderBy(t => t.Percentage))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            var checkBox = new CheckBox
            {
                Content = $"{threshold.Percentage}%",
                IsChecked = threshold.Enabled,
                Width = 100,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += (_, _) => SetThresholdEnabled(metric, threshold.Percentage, true);
            checkBox.Unchecked += (_, _) => SetThresholdEnabled(metric, threshold.Percentage, false);

            var removeButton = new Wpf.Ui.Controls.Button { Content = "Remove" };
            removeButton.Click += (_, _) => RemoveThreshold(metric, threshold.Percentage);

            row.Children.Add(checkBox);
            row.Children.Add(removeButton);
            panel.Children.Add(row);
        }
    }

    private void SetThresholdEnabled(NotificationMetric metric, int percentage, bool enabled)
    {
        if (_isUpdatingProgrammatically)
            return;

        try
        {
            var settings = _settingsStore.Load();
            var threshold = settings.Thresholds.Single(t => t.Metric == metric && t.Percentage == percentage);
            threshold.Enabled = enabled;
            _settingsStore.Save(settings);
        }
        catch (NotificationSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void RemoveThreshold(NotificationMetric metric, int percentage)
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.Thresholds.RemoveAll(t => t.Metric == metric && t.Percentage == percentage);
            _settingsStore.Save(settings);
            LoadAndRender();
        }
        catch (NotificationSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void AddThreshold(NotificationMetric metric, System.Windows.Controls.TextBox input)
    {
        ErrorText.Text = string.Empty;

        if (!int.TryParse(input.Text, out var percentage) || percentage is < 1 or > 100)
        {
            ErrorText.Text = "Enter a whole number between 1 and 100.";
            return;
        }

        try
        {
            var settings = _settingsStore.Load();
            if (settings.Thresholds.Any(t => t.Metric == metric && t.Percentage == percentage))
            {
                ErrorText.Text = $"A {percentage}% threshold already exists for this metric.";
                return;
            }

            settings.Thresholds.Add(new NotificationThreshold { Metric = metric, Percentage = percentage, Enabled = true });
            _settingsStore.Save(settings);
            input.Text = string.Empty;
            LoadAndRender();
        }
        catch (NotificationSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void AddSessionThreshold_Click(object sender, RoutedEventArgs e) =>
        AddThreshold(NotificationMetric.Session, SessionNewThresholdInput);

    private void AddWeeklyThreshold_Click(object sender, RoutedEventArgs e) =>
        AddThreshold(NotificationMetric.Weekly, WeeklyNewThresholdInput);

    private void NotificationsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProgrammatically)
            return;

        try
        {
            var settings = _settingsStore.Load();
            settings.NotificationsEnabled = NotificationsEnabledToggle.IsChecked == true;
            _settingsStore.Save(settings);
        }
        catch (NotificationSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void SoundEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProgrammatically)
            return;

        try
        {
            var settings = _settingsStore.Load();
            settings.SoundEnabled = SoundEnabledToggle.IsChecked == true;
            _settingsStore.Save(settings);
        }
        catch (NotificationSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
