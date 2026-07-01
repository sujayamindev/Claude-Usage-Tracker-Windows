using System.Windows;
using ClaudeUsageTracker.Windows.Services;
using ClaudeUsageTracker.Windows.ViewModels;
using ClaudeUsageTracker.Windows.Views;

namespace ClaudeUsageTracker.Windows;

public partial class App : Application
{
    private readonly WebView2ApiTransport _transport = new();
    private ClaudeApiClient _apiClient = null!;
    private UsageViewModel _viewModel = null!;
    private UsagePollingService _pollingService = null!;
    private TrayIconService _trayIconService = null!;
    private PopoverWindow _popoverWindow = null!;

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

        if (CredentialStore.TryLoad(out var credentials) && credentials is not null)
        {
            _pollingService.Start(credentials.SessionKey, credentials.OrganizationId);
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

    private void RunSetupFlow()
    {
        _popoverWindow.Hide();

        var setupWindow = new SetupWindow(_apiClient);
        var connected = setupWindow.ShowDialog() == true && setupWindow.Result is not null;

        if (connected)
        {
            var credentials = setupWindow.Result!;
            _pollingService.Start(credentials.SessionKey, credentials.OrganizationId);
        }
        else if (!CredentialStore.TryLoad(out _))
        {
            // No stored credentials and the user cancelled setup — nothing to run for.
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pollingService?.Dispose();
        _trayIconService?.Dispose();
        _transport.Dispose();
        base.OnExit(e);
    }
}
