using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.ViewModels;

/// <summary>Shared, observable view of the latest usage snapshot, updated by UsagePollingService.</summary>
public sealed class UsageViewModel : INotifyPropertyChanged
{
    private double _sessionPercentage;
    private double _weeklyPercentage;
    private double _opusWeeklyPercentage;
    private double _sonnetWeeklyPercentage;
    private DateTimeOffset _sessionResetTime = DateTimeOffset.Now.AddHours(5);
    private DateTimeOffset _weeklyResetTime = DateTimeOffset.Now.AddDays(7);
    private bool _isStale;
    private bool _hasAuthError;
    private DateTimeOffset _lastUpdatedAt = DateTimeOffset.Now;
    private string? _accountName;
    private string _statusDescription = "";
    private ClaudeStatusIndicator _statusIndicator = ClaudeStatusIndicator.Unknown;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double SessionPercentage
    {
        get => _sessionPercentage;
        private set => SetField(ref _sessionPercentage, value);
    }

    public double WeeklyPercentage
    {
        get => _weeklyPercentage;
        private set => SetField(ref _weeklyPercentage, value);
    }

    public double OpusWeeklyPercentage
    {
        get => _opusWeeklyPercentage;
        private set => SetField(ref _opusWeeklyPercentage, value);
    }

    public double SonnetWeeklyPercentage
    {
        get => _sonnetWeeklyPercentage;
        private set => SetField(ref _sonnetWeeklyPercentage, value);
    }

    public DateTimeOffset SessionResetTime
    {
        get => _sessionResetTime;
        private set => SetField(ref _sessionResetTime, value);
    }

    public DateTimeOffset WeeklyResetTime
    {
        get => _weeklyResetTime;
        private set => SetField(ref _weeklyResetTime, value);
    }

    /// <summary>True when the last poll failed and the displayed values are stale.</summary>
    public bool IsStale
    {
        get => _isStale;
        private set => SetField(ref _isStale, value);
    }

    /// <summary>True when the session key was rejected (401/403) and setup should re-run.</summary>
    public bool HasAuthError
    {
        get => _hasAuthError;
        private set => SetField(ref _hasAuthError, value);
    }

    /// <summary>When the last successful poll landed, used for the "Updated Xm ago" popover text.</summary>
    public DateTimeOffset LastUpdatedAt
    {
        get => _lastUpdatedAt;
        private set => SetField(ref _lastUpdatedAt, value);
    }

    public UsageStatusLevel SessionStatus => UsageStatusCalculator.CalculateStatus(SessionPercentage);
    public UsageStatusLevel WeeklyStatus => UsageStatusCalculator.CalculateStatus(WeeklyPercentage);

    /// <summary>Organization/workspace name from the Claude account, set once when polling starts.</summary>
    public string? AccountName
    {
        get => _accountName;
        set => SetField(ref _accountName, value);
    }

    public string StatusDescription
    {
        get => _statusDescription;
        set => SetField(ref _statusDescription, value);
    }

    public ClaudeStatusIndicator StatusIndicator
    {
        get => _statusIndicator;
        set => SetField(ref _statusIndicator, value);
    }

    public void ApplyStatus(ClaudeStatus status)
    {
        StatusDescription = status.Description;
        StatusIndicator = status.Indicator;
    }

    public void ApplyUsage(ClaudeUsage usage)
    {
        SessionPercentage = usage.EffectiveSessionPercentage;
        WeeklyPercentage = usage.WeeklyPercentage;
        OpusWeeklyPercentage = usage.OpusWeeklyPercentage;
        SonnetWeeklyPercentage = usage.SonnetWeeklyPercentage;
        SessionResetTime = usage.SessionResetTime;
        WeeklyResetTime = usage.WeeklyResetTime;
        LastUpdatedAt = DateTimeOffset.Now;
        IsStale = false;
        HasAuthError = false;
        OnPropertyChanged(nameof(SessionStatus));
        OnPropertyChanged(nameof(WeeklyStatus));
    }

    public void MarkStale() => IsStale = true;

    public void MarkAuthError() => HasAuthError = true;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
