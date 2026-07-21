using System.Windows.Threading;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.ViewModels;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Timer-driven background refresh, ported from the 30s menu-bar refresh interval in the
/// macOS app (Constants.RefreshIntervals.menuBar). Runs on a DispatcherTimer so ViewModel
/// updates land on the UI thread without manual marshaling.
///
/// Supports two credential modes: a manually-configured session key (StartWithSessionKey), or a
/// Claude Code CLI OAuth fallback (StartWithCliOAuth) used when no session key is configured. The
/// CLI credentials file is re-read fresh on every poll tick rather than cached, so it stays in
/// sync with Claude Code's own token refresh and immediately detects a revoked/expired login.
/// </summary>
public sealed class UsagePollingService : IDisposable
{
    private readonly ClaudeApiClient _apiClient;
    private readonly ClaudeStatusService _statusService = new();
    private readonly UsageViewModel _viewModel;
    private readonly CliCredentialReader _cliCredentialReader;
    private readonly StatuslineInstaller _statuslineInstaller;
    private readonly StatuslineCache _statuslineCache;
    private readonly ThresholdNotifier _thresholdNotifier;
    private readonly UsageHistoryService _usageHistoryService;
    private readonly ResetDetector _resetDetector = new();
    private readonly DispatcherTimer _timer;
    private string? _sessionKey;
    private string? _organizationId;
    private bool _useCliOAuth;
    private Guid _profileId;
    private ClaudeUsage? _lastUsage;

    public event EventHandler? AuthenticationFailed;
    public event EventHandler<NotificationEvent>? ThresholdCrossed;

    public UsagePollingService(
        ClaudeApiClient apiClient,
        UsageViewModel viewModel,
        CliCredentialReader cliCredentialReader,
        StatuslineInstaller statuslineInstaller,
        StatuslineCache statuslineCache,
        ThresholdNotifier thresholdNotifier,
        UsageHistoryService usageHistoryService)
    {
        _apiClient = apiClient;
        _viewModel = viewModel;
        _cliCredentialReader = cliCredentialReader;
        _statuslineInstaller = statuslineInstaller;
        _statuslineCache = statuslineCache;
        _thresholdNotifier = thresholdNotifier;
        _usageHistoryService = usageHistoryService;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public void StartWithSessionKey(Guid profileId, string sessionKey, string organizationId, string? organizationName = null)
    {
        _useCliOAuth = false;
        _sessionKey = sessionKey;
        _organizationId = organizationId;
        _viewModel.AccountName = organizationName;
        ResetHistoryTracking(profileId);
        _timer.Start();
        _ = PollAsync();
    }

    public void StartWithCliOAuth(Guid profileId)
    {
        _useCliOAuth = true;
        _sessionKey = null;
        _organizationId = null;
        _viewModel.AccountName = "Claude Code account";
        ResetHistoryTracking(profileId);
        _timer.Start();
        _ = PollAsync();
    }

    private void ResetHistoryTracking(Guid profileId)
    {
        _profileId = profileId;
        _resetDetector.Reset();
        _lastUsage = null;
    }

    public void Stop() => _timer.Stop();

    public Task RefreshNowAsync() => PollAsync();

    private async Task PollAsync()
    {
        try
        {
            ClaudeUsage usage;

            if (_useCliOAuth)
            {
                var cliCredentials = _cliCredentialReader.TryRead();
                if (cliCredentials is null || cliCredentials.IsExpired)
                {
                    _viewModel.MarkAuthError();
                    Stop();
                    AuthenticationFailed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                usage = await _apiClient.FetchUsageDataViaCliOAuthAsync(cliCredentials.AccessToken);
            }
            else
            {
                if (_sessionKey is null || _organizationId is null)
                    return;

                usage = await _apiClient.FetchUsageDataAsync(_sessionKey, _organizationId);
            }

            RecordUsageHistory(usage);
            _viewModel.ApplyUsage(usage);

            foreach (var notificationEvent in _thresholdNotifier.Evaluate(usage))
                ThresholdCrossed?.Invoke(this, notificationEvent);

            try
            {
                if (_statuslineInstaller.IsEnabled())
                    _statuslineCache.Write(usage);
            }
            catch (StatuslineSettingsException)
            {
                // ~/.claude/settings.json is corrupt; skip the cache write, usage polling continues.
            }
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

    private void RecordUsageHistory(ClaudeUsage usage)
    {
        var previousUsage = _lastUsage;
        var sessionResetDetected = _resetDetector.CheckSessionReset(usage.SessionResetTime);
        var weeklyResetDetected = _resetDetector.CheckWeeklyReset(usage.WeeklyResetTime);

        if (sessionResetDetected)
            _usageHistoryService.RecordSessionReset(_profileId, previousUsage, previousUsage?.SessionResetTime ?? DateTimeOffset.Now);
        else
            _usageHistoryService.RecordSessionPeriodic(_profileId, usage);

        if (weeklyResetDetected)
            _usageHistoryService.RecordWeeklyReset(_profileId, previousUsage, previousUsage?.WeeklyResetTime ?? DateTimeOffset.Now);
        else
            _usageHistoryService.RecordWeeklyPeriodic(_profileId, usage);

        _lastUsage = usage;
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
