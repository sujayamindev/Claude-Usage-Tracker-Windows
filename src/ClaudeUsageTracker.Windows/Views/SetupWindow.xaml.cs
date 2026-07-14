using System.Windows;
using System.Windows.Threading;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class SetupWindow : FluentWindow
{
    private readonly ClaudeApiClient _apiClient;
    private readonly CliCredentialReader _cliCredentialReader;
    private readonly DispatcherTimer? _cliWatchTimer;

    public StoredCredentials? Result { get; private set; }
    public bool CliLoginDetected { get; private set; }

    public SetupWindow(ClaudeApiClient apiClient, CliCredentialReader cliCredentialReader, bool watchForCliLogin)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _apiClient = apiClient;
        _cliCredentialReader = cliCredentialReader;

        if (watchForCliLogin)
        {
            _cliWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _cliWatchTimer.Tick += (_, _) => CheckForCliLogin();
            _cliWatchTimer.Start();
        }

        Closed += (_, _) => _cliWatchTimer?.Stop();
    }

    private void CheckForCliLogin()
    {
        if (_cliCredentialReader.TryRead() is not { IsExpired: false })
            return;

        _cliWatchTimer!.Stop();
        CliLoginDetected = true;
        DialogResult = true;
        Close();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        ConnectButton.IsEnabled = false;

        try
        {
            var sessionKey = SessionKeyValidator.Validate(SessionKeyBox.Text);
            var organizations = await _apiClient.FetchOrganizationsAsync(sessionKey);
            var organization = organizations[0];

            Result = new StoredCredentials(sessionKey, organization.Uuid, organization.Name);
            DialogResult = true;
            Close();
        }
        catch (SessionKeyValidationException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (ClaudeApiException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }
}
