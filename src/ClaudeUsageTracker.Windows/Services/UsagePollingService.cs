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
    private readonly ClaudeStatusService _statusService = new();
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

    public void Start(string sessionKey, string organizationId, string? organizationName = null)
    {
        _sessionKey = sessionKey;
        _organizationId = organizationId;
        _viewModel.AccountName = organizationName;
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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UsagePollingService] Poll failed: {ex}");
            // Network failure / malformed response: keep showing the last known-good value.
            _viewModel.MarkStale();
        }

        await PollStatusAsync();
    }

    private async Task PollStatusAsync()
    {
        try
        {
            var status = await _statusService.FetchStatusAsync();
            _viewModel.ApplyStatus(status);
        }
        catch (Exception ex)
        {
            // Supplementary info only — a failed status fetch shouldn't affect usage polling.
            Console.Error.WriteLine($"[UsagePollingService] Status poll failed: {ex}");
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _statusService.Dispose();
    }
}
