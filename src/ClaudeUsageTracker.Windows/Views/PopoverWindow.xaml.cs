using System.Windows;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;

namespace ClaudeUsageTracker.Windows.Views;

public partial class PopoverWindow : Window
{
    private readonly UsageViewModel _viewModel;
    private readonly UsagePollingService _pollingService;

    public event EventHandler? SignOutRequested;

    public PopoverWindow(UsageViewModel viewModel, UsagePollingService pollingService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _pollingService = pollingService;
        DataContext = viewModel;

        _viewModel.PropertyChanged += (_, _) => Render();
        Render();
    }

    private void Render()
    {
        SessionBar.Value = _viewModel.SessionPercentage;
        SessionPercentText.Text = $"{_viewModel.SessionPercentage:0}% used";
        WeeklyBar.Value = _viewModel.WeeklyPercentage;
        WeeklyPercentText.Text = $"{_viewModel.WeeklyPercentage:0}% used";
        StaleBanner.Visibility = _viewModel.IsStale ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Anchors near the bottom-right of the work area, where the tray typically sits.</summary>
    public void ShowNearTrayIcon()
    {
        Opacity = 0;
        Show();
        UpdateLayout();

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - ActualHeight - 12;

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
