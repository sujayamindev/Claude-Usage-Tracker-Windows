using System.Windows;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class TrayIconStyleWindow : FluentWindow
{
    private readonly TrayIconSettingsStore _settingsStore;
    private readonly Action _onStyleChanged;
    private bool _isUpdatingProgrammatically;

    public TrayIconStyleWindow(TrayIconSettingsStore settingsStore, Action onStyleChanged)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _settingsStore = settingsStore;
        _onStyleChanged = onStyleChanged;
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
        if (_isUpdatingProgrammatically)
            return;

        var style = sender switch
        {
            System.Windows.Controls.RadioButton r when r == StyleProgressBar => TrayIconStyle.ProgressBar,
            System.Windows.Controls.RadioButton r when r == StyleCompact     => TrayIconStyle.Compact,
            _                                                                 => TrayIconStyle.ProgressRing
        };

        try
        {
            ErrorText.Text = string.Empty;
            var settings = _settingsStore.Load();
            settings.Style = style;
            _settingsStore.Save(settings);
            _onStyleChanged();
        }
        catch (TrayIconSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
