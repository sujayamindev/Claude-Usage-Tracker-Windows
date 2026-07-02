using System.Windows;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;
using ClaudeUsageTracker.Windows.Views;

namespace ClaudeUsageTracker.Windows;

public partial class App : Application
{
    private readonly WebView2ApiTransport _transport = new();
    private readonly UpdateService _updateService = new();
    private ClaudeApiClient _apiClient = null!;
    private UsageViewModel _viewModel = null!;
    private UsagePollingService _pollingService = null!;
    private TrayIconService _trayIconService = null!;
    private PopoverWindow _popoverWindow = null!;
    private UpdateCheckResult? _pendingUpdateResult;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _transport.InitializeAsync();

        _apiClient = new ClaudeApiClient(_transport);
        _viewModel = new UsageViewModel();
        _pollingService = new UsagePollingService(_apiClient, _viewModel);
        _pollingService.AuthenticationFailed += (_, _) => Dispatcher.Invoke(RunSetupFlow);

        _popoverWindow = new PopoverWindow(_viewModel, _pollingService);
        _popoverWindow.SignOutRequested += (_, _) =>
        {
            _pollingService.Stop();
            CredentialStore.Clear();
            RunSetupFlow();
        };

        _trayIconService = new TrayIconService(_viewModel);
        _trayIconService.Clicked += (_, _) => OnTrayIconClicked();
        _trayIconService.ExitRequested += (_, _) => Shutdown();
        _trayIconService.CheckForUpdatesRequested += (_, _) => _ = CheckForUpdatesAsync(interactive: true);
        _trayIconService.UpdateNotificationClicked += (_, _) => PromptInstallPendingUpdate();

        if (CredentialStore.TryLoad(out var credentials) && credentials is not null)
        {
            _pollingService.Start(credentials.SessionKey, credentials.OrganizationId, credentials.OrganizationName);
        }
        else
        {
            RunSetupFlow();
        }

        _ = CheckForUpdatesAsync(interactive: false);
    }

    private void OnTrayIconClicked()
    {
        if (_viewModel.HasAuthError)
        {
            RunSetupFlow();
            return;
        }

        if (_popoverWindow.IsVisible)
            _popoverWindow.Hide();
        else
            _popoverWindow.ShowNearTrayIcon();
    }

    private void RunSetupFlow()
    {
        _popoverWindow.Hide();

        var setupWindow = new SetupWindow(_apiClient);
        var connected = setupWindow.ShowDialog() == true && setupWindow.Result is not null;

        if (connected)
        {
            var credentials = setupWindow.Result!;
            _pollingService.Start(credentials.SessionKey, credentials.OrganizationId, credentials.OrganizationName);
        }
        else if (!CredentialStore.TryLoad(out _))
        {
            // No stored credentials and the user cancelled setup — nothing to run for.
            Shutdown();
        }
    }

    private async Task CheckForUpdatesAsync(bool interactive)
    {
        var result = await _updateService.CheckForUpdateAsync();

        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                _pendingUpdateResult = result;
                if (interactive)
                    Dispatcher.Invoke(PromptInstallPendingUpdate);
                else
                    Dispatcher.Invoke(() => _trayIconService.ShowUpdateAvailableNotification(result.LatestVersion!));
                break;

            case UpdateCheckStatus.UpToDate when interactive:
                Dispatcher.Invoke(() => MessageBox.Show(
                    $"You're up to date (v{UpdateService.GetCurrentVersion()}).",
                    "Claude Usage Tracker", MessageBoxButton.OK, MessageBoxImage.Information));
                break;

            case UpdateCheckStatus.Error when interactive:
                Dispatcher.Invoke(() => MessageBox.Show(
                    $"Couldn't check for updates: {result.ErrorMessage}",
                    "Claude Usage Tracker", MessageBoxButton.OK, MessageBoxImage.Warning));
                break;
        }
    }

    private void PromptInstallPendingUpdate()
    {
        if (_pendingUpdateResult is not { Status: UpdateCheckStatus.UpdateAvailable } result)
            return;

        var choice = MessageBox.Show(
            $"Version {result.LatestVersion} is available. Install now?",
            "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
            return;

        _ = InstallUpdateAsync(result.DownloadUrl!);
    }

    private async Task InstallUpdateAsync(string downloadUrl)
    {
        try
        {
            await _updateService.DownloadAndInstallAsync(downloadUrl);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => MessageBox.Show(
                $"Couldn't install the update: {ex.Message}",
                "Claude Usage Tracker", MessageBoxButton.OK, MessageBoxImage.Warning));
            return;
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pollingService?.Dispose();
        _trayIconService?.Dispose();
        _transport.Dispose();
        _updateService.Dispose();
        base.OnExit(e);
    }
}
