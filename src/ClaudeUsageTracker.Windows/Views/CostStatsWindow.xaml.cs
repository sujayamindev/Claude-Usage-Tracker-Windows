using System.Windows;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class CostStatsWindow : FluentWindow
{
    private sealed record DailyTotalRow(string DateLabel, string CostLabel);

    private readonly CostLedgerService _costLedgerService;

    public CostStatsWindow(CostLedgerService costLedgerService)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);
        _costLedgerService = costLedgerService;

        Render();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Render();

    private void Render()
    {
        var entries = _costLedgerService.LoadAndCompact();

        if (entries.Count == 0)
        {
            TotalText.Text = "$0.00 total";
            EmptyStateText.Visibility = Visibility.Visible;
            DailyTotalsListView.ItemsSource = null;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        TotalText.Text = $"{_costLedgerService.GetAllTimeTotal(entries):C} total";

        DailyTotalsListView.ItemsSource = _costLedgerService.GetDailyTotals(entries)
            .Select(d => new DailyTotalRow(
                d.Date.ToString("MMM d, yyyy"),
                d.Total.ToString("C")))
            .ToList();
    }
}
