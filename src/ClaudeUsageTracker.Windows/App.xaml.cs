using System.Windows;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;
using ClaudeUsageTracker.Windows.Views;

namespace ClaudeUsageTracker.Windows;

public partial class App : Application
{
    private readonly WebView2ApiTransport _transport = new();
    private readonly UpdateService _updateService = new();
    private readonly CliCredentialReader _cliCredentialReader = new();
    private readonly StatuslineInstaller _statuslineInstaller = new();
    private readonly StatuslineCache _statuslineCache = new();
    private readonly NotificationSettingsStore _notificationSettingsStore = new();
    private readonly TrayIconSettingsStore _trayIconSettingsStore = new();
    private readonly ProfileStore _profileStore = new();
    private ProfileManager _profileManager = null!;
    private ThresholdNotifier _thresholdNotifier = null!;
    private ClaudeApiClient _apiClient = null!;
    private UsageViewModel _viewModel = null!;
    private UsagePollingService _pollingService = null!;
    private TrayIconService _trayIconService = null!;
    private PopoverWindow _popoverWindow = null!;
    private DetachedUsageWindow? _detachedWindow;
    private UpdateCheckResult? _pendingUpdateResult;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--statusline"))
        {
            RunStatuslineMode();
            Shutdown();
            return;
        }

        _thresholdNotifier = new ThresholdNotifier(_notificationSettingsStore);

        try
        {
            _profileManager = new ProfileManager(_profileStore, _cliCredentialReader, new UsageHistoryService());
        }
        catch (ProfileStoreException ex)
        {
            MessageBox.Show(
                $"{ex.Message}\n\nClaude Usage Tracker can't start until this is fixed.",
                "Claude Usage Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        await _transport.InitializeAsync();

        _apiClient = new ClaudeApiClient(_transport);
        _viewModel = new UsageViewModel();
        _pollingService = new UsagePollingService(_apiClient, _viewModel, _cliCredentialReader, _statuslineInstaller, _statuslineCache, _thresholdNotifier, new UsageHistoryService());
        _pollingService.AuthenticationFailed += (_, _) => Dispatcher.Invoke(() => RunSetupFlow());
        _pollingService.ThresholdCrossed += (_, evt) => Dispatcher.Invoke(() => _trayIconService.ShowThresholdNotification(evt));
        _profileManager.ActiveProfileChanged += (_, profile) => Dispatcher.Invoke(() => SwitchToProfile(profile));

        _popoverWindow = new PopoverWindow(_viewModel, _pollingService, _trayIconSettingsStore);
        _popoverWindow.SignOutRequested += (_, _) =>
        {
            _pollingService.Stop();
            CredentialStore.Clear(_profileManager.ActiveProfile.Id);
            RunSetupFlow(watchForCliLogin: false);
        };
        _popoverWindow.DetachRequested += (_, _) =>
        {
            _popoverWindow.Hide();
            if (_detachedWindow?.IsVisible == true)
            {
                _detachedWindow.Activate();
                return;
            }
            _detachedWindow = new DetachedUsageWindow(_viewModel, _pollingService, _trayIconSettingsStore);
            _detachedWindow.SignOutRequested += (_, _) =>
            {
                _pollingService.Stop();
                CredentialStore.Clear(_profileManager.ActiveProfile.Id);
                RunSetupFlow(watchForCliLogin: false);
            };
            _detachedWindow.Closed += (_, _) => _detachedWindow = null;
            _detachedWindow.Show();
        };

        _trayIconService = new TrayIconService(_viewModel, _notificationSettingsStore, _trayIconSettingsStore);
        _trayIconService.Clicked += (_, _) => OnTrayIconClicked();
        _trayIconService.ExitRequested += (_, _) => Shutdown();
        _trayIconService.CheckForUpdatesRequested += (_, _) => _ = CheckForUpdatesAsync(interactive: true);
        _trayIconService.UpdateNotificationClicked += (_, _) => PromptInstallPendingUpdate();
        _trayIconService.StatuslineSettingsRequested += (_, _) => OpenStatuslineSettings();
        _trayIconService.NotificationSettingsRequested += (_, _) => OpenNotificationSettings();
        _trayIconService.IconStyleSettingsRequested += (_, _) => OpenAppearanceSettings();
        _trayIconService.ManageProfilesRequested += (_, _) => OpenManageProfiles();

        SwitchToProfile(_profileManager.ActiveProfile);

        _ = CheckForUpdatesAsync(interactive: false);
    }

    private void RunStatuslineMode()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var input = StatuslineInput.TryParse(Console.In.ReadToEnd());
        var cacheEntry = _statuslineCache.TryRead(TimeSpan.FromSeconds(90));
        Console.WriteLine(StatuslineFormatter.Format(input, cacheEntry));
    }

    private void SwitchToProfile(Models.Profile profile)
    {
        _pollingService.Stop();

        if (profile.AuthMode == Models.ProfileAuthMode.CliOAuth)
        {
            _pollingService.StartWithCliOAuth(profile.Id);
        }
        else if (CredentialStore.TryLoad(profile.Id, out var creds) && creds is not null)
        {
            _pollingService.StartWithSessionKey(profile.Id, creds.SessionKey, creds.OrganizationId, creds.OrganizationName);
        }
        else
        {
            RunSetupFlow();
        }
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

    private void OpenStatuslineSettings()
    {
        var window = new StatuslineSettingsWindow(_statuslineInstaller, _viewModel);
        window.ShowDialog();
    }

    private void OpenNotificationSettings()
    {
        var window = new NotificationSettingsWindow(_notificationSettingsStore);
        window.ShowDialog();
    }

    private void OpenAppearanceSettings()
    {
        var window = new TrayIconStyleWindow(_trayIconSettingsStore, () => _trayIconService.TriggerRender());
        window.ShowDialog();
    }

    private void OpenManageProfiles()
    {
        var window = new ManageProfilesWindow(_profileManager, _apiClient, _cliCredentialReader);
        window.ShowDialog();
    }

    private void RunSetupFlow(bool watchForCliLogin = true)
    {
        _popoverWindow.Hide();

        var setupWindow = new SetupWindow(_apiClient, _cliCredentialReader, watchForCliLogin);
        var shown = setupWindow.ShowDialog() == true;

        if (shown && setupWindow.CliLoginDetected)
        {
            _profileManager.UpdateActiveProfileCredentials(Models.ProfileAuthMode.CliOAuth, null);
            _pollingService.StartWithCliOAuth(_profileManager.ActiveProfile.Id);
        }
        else if (shown && setupWindow.Result is not null)
        {
            var credentials = setupWindow.Result!;
            _profileManager.UpdateActiveProfileCredentials(Models.ProfileAuthMode.SessionKey, credentials);
            _pollingService.StartWithSessionKey(_profileManager.ActiveProfile.Id, credentials.SessionKey, credentials.OrganizationId, credentials.OrganizationName);
        }
        else if (!CredentialStore.TryLoad(_profileManager.ActiveProfile.Id, out _))
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
