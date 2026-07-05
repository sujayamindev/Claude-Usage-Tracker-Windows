using System.Windows;
using System.Windows.Media;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class TrayIconStyleWindow : FluentWindow
{
    private readonly TrayIconSettingsStore _settingsStore;
    private readonly Action _onSettingsChanged;
    private bool _isUpdatingProgrammatically;

    public TrayIconStyleWindow(TrayIconSettingsStore settingsStore, Action onSettingsChanged)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _settingsStore = settingsStore;
        _onSettingsChanged = onSettingsChanged;
        Load();
    }

    private void Load()
    {
        try
        {
            _isUpdatingProgrammatically = true;
            ErrorText.Text = string.Empty;

            var settings = _settingsStore.Load();

            StyleProgressRing.IsChecked = settings.Style == TrayIconStyle.ProgressRing;
            StyleProgressBar.IsChecked  = settings.Style == TrayIconStyle.ProgressBar;
            StyleCompact.IsChecked      = settings.Style == TrayIconStyle.Compact;

            ColorMulti.IsChecked  = settings.ColorMode == TrayIconColorMode.MultiColor;
            ColorMono.IsChecked   = settings.ColorMode == TrayIconColorMode.Monochrome;
            ColorSingle.IsChecked = settings.ColorMode == TrayIconColorMode.SingleColor;

            SingleColorHexBox.Text = settings.SingleColorHex;
            UpdateColorSwatch(settings.SingleColorHex);

            SingleColorPickerHost.Visibility = settings.ColorMode == TrayIconColorMode.SingleColor
                ? Visibility.Visible
                : Visibility.Collapsed;

            PaceMarkerCheck.IsChecked = settings.ShowPaceMarker;
            RemainingCheck.IsChecked  = settings.ShowRemainingPercentage;
        }
        catch (TrayIconSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            _isUpdatingProgrammatically = false;
        }
    }

    private void Style_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProgrammatically) return;

        var style = sender switch
        {
            System.Windows.Controls.RadioButton r when r == StyleProgressBar => TrayIconStyle.ProgressBar,
            System.Windows.Controls.RadioButton r when r == StyleCompact     => TrayIconStyle.Compact,
            _                                                                 => TrayIconStyle.ProgressRing
        };

        SaveAndNotify(s => s.Style = style);
    }

    private void Color_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProgrammatically) return;

        var mode = sender switch
        {
            System.Windows.Controls.RadioButton r when r == ColorMono   => TrayIconColorMode.Monochrome,
            System.Windows.Controls.RadioButton r when r == ColorSingle => TrayIconColorMode.SingleColor,
            _                                                            => TrayIconColorMode.MultiColor
        };

        SingleColorPickerHost.Visibility = mode == TrayIconColorMode.SingleColor
            ? Visibility.Visible
            : Visibility.Collapsed;

        SaveAndNotify(s => s.ColorMode = mode);
    }

    private void SingleColorHex_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdatingProgrammatically) return;

        var hex = SingleColorHexBox.Text?.Trim() ?? string.Empty;
        if (!TryParseHexColor(hex, out _))
            return; // wait until it's a valid color before persisting

        UpdateColorSwatch(hex);
        SaveAndNotify(s => s.SingleColorHex = hex);
    }

    private void PaceMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProgrammatically) return;
        SaveAndNotify(s => s.ShowPaceMarker = PaceMarkerCheck.IsChecked == true);
    }

    private void Remaining_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProgrammatically) return;
        SaveAndNotify(s => s.ShowRemainingPercentage = RemainingCheck.IsChecked == true);
    }

    private void SaveAndNotify(Action<TrayIconSettings> mutate)
    {
        try
        {
            ErrorText.Text = string.Empty;
            var settings = _settingsStore.Load();
            mutate(settings);
            _settingsStore.Save(settings);
            _onSettingsChanged();
        }
        catch (TrayIconSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void UpdateColorSwatch(string hex)
    {
        if (TryParseHexColor(hex, out var color))
            ColorPreviewSwatch.Background = new SolidColorBrush(color);
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
