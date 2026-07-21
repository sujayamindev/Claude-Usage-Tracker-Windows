using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Win32;
using SkiaSharp;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class UsageHistoryWindow : FluentWindow
{
    private sealed record TimeScaleOption(ChartTimeScale Value, string Label);

    private static readonly TimeScaleOption[] TimeScaleOptions =
    [
        new(ChartTimeScale.Hours5, ChartTimeScale.Hours5.Label()),
        new(ChartTimeScale.Hours24, ChartTimeScale.Hours24.Label()),
        new(ChartTimeScale.Days7, ChartTimeScale.Days7.Label()),
        new(ChartTimeScale.Days30, ChartTimeScale.Days30.Label())
    ];

    private readonly UsageHistoryService _historyService;
    private readonly Guid _profileId;
    private readonly UsageViewModel _viewModel;
    private readonly System.ComponentModel.PropertyChangedEventHandler _propertyChangedHandler;

    private ChartTimeScale _timeScale = ChartTimeScale.Hours24;
    private double _timeOffsetHours;

    public UsageHistoryWindow(UsageHistoryService historyService, Guid profileId, UsageViewModel viewModel)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);
        _historyService = historyService;
        _profileId = profileId;
        _viewModel = viewModel;

        TimeScaleComboBox.ItemsSource = TimeScaleOptions;
        TimeScaleComboBox.DisplayMemberPath = nameof(TimeScaleOption.Label);
        TimeScaleComboBox.SelectedIndex = 1; // Hours24, matches macOS default

        _propertyChangedHandler = (_, _) => RenderChart();
        _viewModel.PropertyChanged += _propertyChangedHandler;
        Closed += (_, _) => _viewModel.PropertyChanged -= _propertyChangedHandler;

        RenderChart();
    }

    private void TimeScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeScaleComboBox.SelectedItem is not TimeScaleOption option)
            return;

        _timeScale = option.Value;
        _timeOffsetHours = 0;
        RenderChart();
    }

    private void RenderChart()
    {
        var history = _historyService.LoadHistory(_profileId);
        var now = DateTimeOffset.Now;
        var range = ChartWindowCalculator.VisibleRange(_timeScale, _timeOffsetHours, now);

        var sessionPoints = history.SessionSnapshots
            .Where(s => s.SessionPercentage is not null && s.Timestamp >= range.Start && s.Timestamp <= range.End)
            .OrderBy(s => s.Timestamp)
            .Select(s => new DateTimePoint(s.Timestamp.LocalDateTime, Math.Clamp(s.SessionPercentage!.Value, 0, 100)))
            .ToList();

        var weeklyPoints = history.WeeklySnapshots
            .Where(s => s.WeeklyPercentage is not null && s.Timestamp >= range.Start && s.Timestamp <= range.End)
            .OrderBy(s => s.Timestamp)
            .Select(s => new DateTimePoint(s.Timestamp.LocalDateTime, Math.Clamp(s.WeeklyPercentage!.Value, 0, 100)))
            .ToList();

        EmptyStateText.Visibility = sessionPoints.Count == 0 && weeklyPoints.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        Chart.Series =
        [
            new LineSeries<DateTimePoint>
            {
                Name = "Session",
                Values = sessionPoints,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(40)),
                GeometrySize = 0,
                LineSmoothness = 0
            },
            new LineSeries<DateTimePoint>
            {
                Name = "Weekly",
                Values = weeklyPoints,
                Stroke = new SolidColorPaint(SKColors.MediumPurple, 2) { PathEffect = new DashEffect([6, 4]) },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            }
        ];

        Chart.XAxes =
        [
            new Axis
            {
                Labeler = value => new DateTime((long)value).ToString(XAxisFormat()),
                MinLimit = range.Start.LocalDateTime.Ticks,
                MaxLimit = range.End.LocalDateTime.Ticks
            }
        ];
        Chart.YAxes =
        [
            new Axis { MinLimit = 0, MaxLimit = 100, Labeler = value => $"{value:0}%" }
        ];

        var oldestTimestamp = history.Snapshots.Count == 0 ? (DateTimeOffset?)null : history.Snapshots.Min(s => s.Timestamp);
        BackButton.IsEnabled = ChartWindowCalculator.CanGoBack(oldestTimestamp, range);
        ForwardButton.IsEnabled = ChartWindowCalculator.CanGoForward(_timeOffsetHours);

        TimeRangeLabel.Text = $"{range.Start.LocalDateTime:MMM d, h:mm tt} – {range.End.LocalDateTime:MMM d, h:mm tt}";
    }

    private string XAxisFormat() => _timeScale switch
    {
        ChartTimeScale.Hours5 or ChartTimeScale.Hours24 => "h:mm tt",
        ChartTimeScale.Days7 => "ddd h tt",
        ChartTimeScale.Days30 => "MMM d",
        _ => "g"
    };

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _timeOffsetHours -= ChartWindowCalculator.StepHours(_timeScale);
        RenderChart();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        _timeOffsetHours += ChartWindowCalculator.StepHours(_timeScale);
        RenderChart();
    }

    private void NowButton_Click(object sender, RoutedEventArgs e)
    {
        _timeOffsetHours = 0;
        RenderChart();
    }

    private void ExportJsonButton_Click(object sender, RoutedEventArgs e) => ExportHistory(HistoryExportFormat.Json, "json");

    private void ExportCsvButton_Click(object sender, RoutedEventArgs e) => ExportHistory(HistoryExportFormat.Csv, "csv");

    private void ExportHistory(HistoryExportFormat format, string extension)
    {
        var content = _historyService.Export(_profileId, format);
        var dialog = new SaveFileDialog
        {
            FileName = $"claude-usage-history-{DateTimeOffset.Now:yyyy-MM-dd}.{extension}",
            Filter = extension == "json" ? "JSON files (*.json)|*.json" : "CSV files (*.csv)|*.csv"
        };

        if (dialog.ShowDialog() == true)
            File.WriteAllText(dialog.FileName, content);
    }
}
