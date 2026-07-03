using System.Windows;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class StatuslineSettingsWindow : FluentWindow
{
    private readonly StatuslineInstaller _statuslineInstaller;
    private readonly UsageViewModel _viewModel;
    private bool _isUpdatingToggleProgrammatically;

    public StatuslineSettingsWindow(StatuslineInstaller statuslineInstaller, UsageViewModel viewModel)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _statuslineInstaller = statuslineInstaller;
        _viewModel = viewModel;

        RefreshPreview();
        LoadInitialToggleState();
    }

    private void LoadInitialToggleState()
    {
        try
        {
            _isUpdatingToggleProgrammatically = true;
            EnabledToggle.IsChecked = _statuslineInstaller.IsEnabled();
        }
        catch (StatuslineSettingsException ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            _isUpdatingToggleProgrammatically = false;
        }
    }

    private void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingToggleProgrammatically)
            return;

        ErrorText.Text = string.Empty;

        try
        {
            if (EnabledToggle.IsChecked == true)
                _statuslineInstaller.Enable();
            else
                _statuslineInstaller.Disable();
        }
        catch (StatuslineSettingsException ex)
        {
            ErrorText.Text = ex.Message;

            _isUpdatingToggleProgrammatically = true;
            EnabledToggle.IsChecked = !EnabledToggle.IsChecked;
            _isUpdatingToggleProgrammatically = false;
        }
    }

    private void RefreshPreview()
    {
        var mockInput = new StatuslineInput("~/your-project", "Sonnet 4.5", 8.0);
        var cacheEntry = new StatuslineCacheEntry(
            _viewModel.SessionPercentage,
            DateTimeOffset.Now.AddHours(2).AddMinutes(14),
            _viewModel.WeeklyPercentage,
            DateTimeOffset.Now.AddDays(3),
            DateTimeOffset.Now);

        PreviewText.Text = StatuslineFormatter.Format(mockInput, cacheEntry, useAnsiColor: false);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
