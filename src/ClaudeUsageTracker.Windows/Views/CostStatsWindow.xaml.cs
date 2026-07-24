using System.Globalization;
using System.IO;
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

    private static readonly CultureInfo UsdCulture = CultureInfo.GetCultureInfo("en-US");

    private void Render()
    {
        IReadOnlyList<Models.CostLedgerEntry> entries;
        try
        {
            entries = _costLedgerService.LoadAndCompact();
        }
        catch (IOException)
        {
            // The ledger file may be locked by the concurrent statusline.ps1 writer at the moment
            // we try to read/compact it. Leave the window's current display as-is (matches
            // UsageViewModel/UsagePollingService's "stale on failure" philosophy) rather than
            // crashing or clearing what's already shown.
            return;
        }

        if (entries.Count == 0)
        {
            TotalText.Text = "$0.00 total";
            EmptyStateText.Visibility = Visibility.Visible;
            DailyTotalsListView.ItemsSource = null;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        TotalText.Text = $"{_costLedgerService.GetAllTimeTotal(entries).ToString("C", UsdCulture)} total";

        DailyTotalsListView.ItemsSource = _costLedgerService.GetDailyTotals(entries)
            .Select(d => new DailyTotalRow(
                d.Date.ToString("MMM d, yyyy"),
                d.Total.ToString("C", UsdCulture)))
            .ToList();
    }
}
