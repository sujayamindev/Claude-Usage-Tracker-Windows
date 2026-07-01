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
    private DateTimeOffset _sessionResetTime = DateTimeOffset.Now.AddHours(5);
    private DateTimeOffset _weeklyResetTime = DateTimeOffset.Now.AddDays(7);
    private bool _isStale;
    private bool _hasAuthError;

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

    public UsageStatusLevel SessionStatus => UsageStatusCalculator.CalculateStatus(SessionPercentage);
    public UsageStatusLevel WeeklyStatus => UsageStatusCalculator.CalculateStatus(WeeklyPercentage);

    public void ApplyUsage(ClaudeUsage usage)
    {
        SessionPercentage = usage.EffectiveSessionPercentage;
        WeeklyPercentage = usage.WeeklyPercentage;
        SessionResetTime = usage.SessionResetTime;
        WeeklyResetTime = usage.WeeklyResetTime;
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
