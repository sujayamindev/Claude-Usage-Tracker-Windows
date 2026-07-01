using System.Windows.Threading;
using ClaudeUsageTracker.Windows.ViewModels;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Timer-driven background refresh, ported from the 30s menu-bar refresh interval in the
/// macOS app (Constants.RefreshIntervals.menuBar). Runs on a DispatcherTimer so ViewModel
/// updates land on the UI thread without manual marshaling.
/// </summary>
public sealed class UsagePollingService : IDisposable
{
    private readonly ClaudeApiClient _apiClient;
    private readonly UsageViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private string? _sessionKey;
    private string? _organizationId;

    public event EventHandler? AuthenticationFailed;

    public UsagePollingService(ClaudeApiClient apiClient, UsageViewModel viewModel)
    {
        _apiClient = apiClient;
        _viewModel = viewModel;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public void Start(string sessionKey, string organizationId)
    {
        _sessionKey = sessionKey;
        _organizationId = organizationId;
        _timer.Start();
        _ = PollAsync();
    }

    public void Stop() => _timer.Stop();

    public Task RefreshNowAsync() => PollAsync();

    private async Task PollAsync()
    {
        if (_sessionKey is null || _organizationId is null)
            return;

        try
        {
            var usage = await _apiClient.FetchUsageDataAsync(_sessionKey, _organizationId);
            _viewModel.ApplyUsage(usage);
        }
        catch (ClaudeApiException ex) when (ex.IsUnauthorized)
        {
            _viewModel.MarkAuthError();
            Stop();
            AuthenticationFailed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Network failure / malformed response: keep showing the last known-good value.
            _viewModel.MarkStale();
        }
    }

    public void Dispose() => _timer.Stop();
}
