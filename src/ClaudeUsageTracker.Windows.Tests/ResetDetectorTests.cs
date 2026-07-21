using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class ResetDetectorTests
{
    [Fact]
    public void CheckSessionReset_FirstCall_RecordsButDoesNotDetectReset()
    {
        var detector = new ResetDetector();

        var detected = detector.CheckSessionReset(DateTimeOffset.Now.AddHours(5));

        Assert.False(detected);
    }

    [Fact]
    public void CheckSessionReset_SameResetTime_NoResetDetected()
    {
        var detector = new ResetDetector();
        var resetTime = DateTimeOffset.Now.AddHours(5);
        detector.CheckSessionReset(resetTime);

        var detected = detector.CheckSessionReset(resetTime);

        Assert.False(detected);
    }

    [Fact]
    public void CheckSessionReset_ResetTimeAdvanced_DetectsReset()
    {
        var detector = new ResetDetector();
        var firstResetTime = DateTimeOffset.Now.AddHours(5);
        detector.CheckSessionReset(firstResetTime);

        var detected = detector.CheckSessionReset(firstResetTime.AddHours(5));

        Assert.True(detected);
    }

    [Fact]
    public void CheckSessionReset_ResetTimeMovedBackward_StillDetectsChange()
    {
        var detector = new ResetDetector();
        var firstResetTime = DateTimeOffset.Now.AddHours(5);
        detector.CheckSessionReset(firstResetTime);

        var detected = detector.CheckSessionReset(firstResetTime.AddHours(-1));

        Assert.True(detected);
    }

    [Fact]
    public void CheckSessionReset_SubMinuteJitter_DoesNotDetectReset()
    {
        var detector = new ResetDetector();
        var resetTime = new DateTimeOffset(2026, 7, 18, 10, 0, 30, TimeSpan.Zero);
        detector.CheckSessionReset(resetTime);

        var detected = detector.CheckSessionReset(resetTime.AddSeconds(20));

        Assert.False(detected);
    }

    [Fact]
    public void Reset_ClearsTrackedState_NextCallDoesNotDetectReset()
    {
        var detector = new ResetDetector();
        detector.CheckSessionReset(DateTimeOffset.Now.AddHours(5));

        detector.Reset();
        var detected = detector.CheckSessionReset(DateTimeOffset.Now.AddHours(10));

        Assert.False(detected);
    }

    [Fact]
    public void WeeklyAndSessionTracking_AreIndependent()
    {
        var detector = new ResetDetector();
        detector.CheckWeeklyReset(DateTimeOffset.Now.AddDays(7));

        var sessionDetected = detector.CheckSessionReset(DateTimeOffset.Now.AddHours(5));

        Assert.False(sessionDetected);
    }
}
