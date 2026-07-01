using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class PopoverWindow : FluentWindow
{
    private static readonly TimeSpan SessionWindowDuration = TimeSpan.FromHours(5);
    private static readonly TimeSpan WeeklyWindowDuration = TimeSpan.FromDays(7);

    private readonly UsageViewModel _viewModel;
    private readonly UsagePollingService _pollingService;

    public event EventHandler? SignOutRequested;

    public PopoverWindow(UsageViewModel viewModel, UsagePollingService pollingService)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.Acrylic, updateAccents: true);
        _viewModel = viewModel;
        _pollingService = pollingService;
        DataContext = viewModel;

        _viewModel.PropertyChanged += (_, _) => Render();
        Render();
    }

    private void Render()
    {
        SessionBar.Value = _viewModel.SessionPercentage;
        SessionPercentText.Text = $"{_viewModel.SessionPercentage:0}%";
        WeeklyBar.Value = _viewModel.WeeklyPercentage;
        WeeklyPercentText.Text = $"{_viewModel.WeeklyPercentage:0}%";
        StaleBanner.Visibility = _viewModel.IsStale ? Visibility.Visible : Visibility.Collapsed;

        AccountNameText.Text = $"Connected as {_viewModel.AccountName}";
        AccountNameText.Visibility = string.IsNullOrWhiteSpace(_viewModel.AccountName) ? Visibility.Collapsed : Visibility.Visible;

        SessionResetText.Text = $"Resets in {FormatTimeRemaining(_viewModel.SessionResetTime - DateTimeOffset.Now)}";
        WeeklyResetText.Text = $"Resets in {FormatTimeRemaining(_viewModel.WeeklyResetTime - DateTimeOffset.Now)}";

        SetElapsedTick(SessionTickElapsedColumn, SessionTickRemainingColumn, _viewModel.SessionResetTime, SessionWindowDuration);
        SetElapsedTick(WeeklyTickElapsedColumn, WeeklyTickRemainingColumn, _viewModel.WeeklyResetTime, WeeklyWindowDuration);

        var minutesAgo = (int)(DateTimeOffset.Now - _viewModel.LastUpdatedAt).TotalMinutes;
        LastUpdatedText.Text = minutesAgo <= 0 ? "Updated just now" : $"Updated {minutesAgo}m ago";

        OpusRow.Visibility = _viewModel.OpusWeeklyPercentage > 0 ? Visibility.Visible : Visibility.Collapsed;
        OpusBar.Value = _viewModel.OpusWeeklyPercentage;
        OpusPercentText.Text = $"{_viewModel.OpusWeeklyPercentage:0}%";

        SonnetRow.Visibility = _viewModel.SonnetWeeklyPercentage > 0 ? Visibility.Visible : Visibility.Collapsed;
        SonnetBar.Value = _viewModel.SonnetWeeklyPercentage;
        SonnetPercentText.Text = $"{_viewModel.SonnetWeeklyPercentage:0}%";

        RenderInsight();
        RenderStatus();
    }

    private void RenderStatus()
    {
        StatusDescriptionText.Text = string.IsNullOrWhiteSpace(_viewModel.StatusDescription)
            ? "Claude Status"
            : _viewModel.StatusDescription;

        StatusDot.Fill = _viewModel.StatusIndicator switch
        {
            ClaudeStatusIndicator.None => new SolidColorBrush(Color.FromRgb(52, 168, 83)),
            ClaudeStatusIndicator.Minor => new SolidColorBrush(Color.FromRgb(251, 200, 0)),
            ClaudeStatusIndicator.Major => new SolidColorBrush(Color.FromRgb(251, 140, 0)),
            ClaudeStatusIndicator.Critical => new SolidColorBrush(Color.FromRgb(217, 48, 37)),
            _ => Brushes.Gray
        };
    }

    private void RenderInsight()
    {
        string? text;
        Brush color;

        if (_viewModel.SessionPercentage > 80)
        {
            text = "Session usage is high";
            color = new SolidColorBrush(Color.FromRgb(251, 140, 0));
        }
        else if (_viewModel.WeeklyPercentage > 90)
        {
            text = "Weekly usage approaching limit";
            color = new SolidColorBrush(Color.FromRgb(217, 48, 37));
        }
        else if (_viewModel.SessionPercentage < 20 && _viewModel.WeeklyPercentage < 30)
        {
            text = "Efficient usage this week";
            color = new SolidColorBrush(Color.FromRgb(52, 168, 83));
        }
        else
        {
            text = null;
            color = Brushes.Transparent;
        }

        InsightText.Text = text ?? string.Empty;
        InsightText.Foreground = color;
        InsightText.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void SetElapsedTick(ColumnDefinition elapsedColumn, ColumnDefinition remainingColumn, DateTimeOffset resetTime, TimeSpan windowDuration)
    {
        var windowStart = resetTime - windowDuration;
        var elapsed = DateTimeOffset.Now - windowStart;
        var fraction = Math.Clamp(elapsed.TotalSeconds / windowDuration.TotalSeconds, 0.0, 1.0);

        elapsedColumn.Width = new GridLength(fraction, GridUnitType.Star);
        remainingColumn.Width = new GridLength(1.0 - fraction, GridUnitType.Star);
    }

    private static string FormatTimeRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return "soon";

        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";

        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m"
            : $"{remaining.Minutes}m";
    }

    private void StatusLinkText_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://status.claude.com") { UseShellExecute = true });

    /// <summary>Anchors near the bottom-right of the work area, where the tray typically sits.</summary>
    public void ShowNearTrayIcon()
    {
        Opacity = 0;
        Show();
        UpdateLayout();

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - ActualHeight - 56;

        Opacity = 1;
        Activate();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await _pollingService.RefreshNowAsync();

    private void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        SignOutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PopoverWindow_Deactivated(object? sender, EventArgs e) => Hide();
}
