using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.ViewModels;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Owns the tray icon, rendering the live session percentage using the user's chosen icon style
/// (ported from the macOS app's MenuBarIconRenderer styles).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int IconSize = 32;
    private double _lastElapsedFraction;

    private readonly TaskbarIcon _taskbarIcon;
    private readonly UsageViewModel _viewModel;
    private readonly NotificationSettingsStore _notificationSettingsStore;
    private readonly TrayIconSettingsStore _trayIconSettingsStore;

    public event EventHandler? Clicked;
    public event EventHandler? ExitRequested;
    public event EventHandler? CheckForUpdatesRequested;
    public event EventHandler? UpdateNotificationClicked;
    public event EventHandler? StatuslineSettingsRequested;
    public event EventHandler? NotificationSettingsRequested;
    public event EventHandler? IconStyleSettingsRequested;

    public TrayIconService(UsageViewModel viewModel, NotificationSettingsStore notificationSettingsStore, TrayIconSettingsStore trayIconSettingsStore)
    {
        _viewModel = viewModel;
        _notificationSettingsStore = notificationSettingsStore;
        _trayIconSettingsStore = trayIconSettingsStore;

        var startupItem = new MenuItem { Header = "Launch at Startup", IsCheckable = true, IsChecked = StartupService.IsEnabled() };
        startupItem.Click += (_, _) => StartupService.SetEnabled(startupItem.IsChecked);

        var iconStyleItem = new MenuItem { Header = "Icon Style…" };
        iconStyleItem.Click += (_, _) => IconStyleSettingsRequested?.Invoke(this, EventArgs.Empty);

        var statuslineItem = new MenuItem { Header = "Statusline Settings…" };
        statuslineItem.Click += (_, _) => StatuslineSettingsRequested?.Invoke(this, EventArgs.Empty);

        var notificationSettingsItem = new MenuItem { Header = "Notification Settings…" };
        notificationSettingsItem.Click += (_, _) => NotificationSettingsRequested?.Invoke(this, EventArgs.Empty);

        var checkForUpdatesItem = new MenuItem { Header = "Check for Updates" };
        checkForUpdatesItem.Click += (_, _) => CheckForUpdatesRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Claude Usage Tracker",
            ContextMenu = new ContextMenu { Items = { startupItem, iconStyleItem, statuslineItem, notificationSettingsItem, checkForUpdatesItem, new Separator(), exitItem } }
        };
        _taskbarIcon.TrayLeftMouseUp += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        _taskbarIcon.TrayBalloonTipClicked += (_, _) => UpdateNotificationClicked?.Invoke(this, EventArgs.Empty);

        // Required when the TaskbarIcon isn't placed in a loaded visual tree/resources (our case,
        // since it's constructed directly in code) — otherwise the tray icon is never actually created.
        _taskbarIcon.ForceCreate();

        _viewModel.PropertyChanged += (_, _) => Render();
        Render();
    }

    public void TriggerRender() => Render();

    public void ShowUpdateAvailableNotification(string version) =>
        _taskbarIcon.ShowNotification("Update available", $"Version {version} is available. Click to install.", NotificationIcon.Info);

    public void ShowThresholdNotification(NotificationEvent notificationEvent)
    {
        var metricLabel = notificationEvent.Metric == NotificationMetric.Session ? "Session" : "Weekly";
        bool soundEnabled;
        try { soundEnabled = _notificationSettingsStore.Load().SoundEnabled; }
        catch (NotificationSettingsException) { soundEnabled = false; }

        _taskbarIcon.ShowNotification(
            $"{metricLabel} usage",
            $"{metricLabel} usage has reached {notificationEvent.Percentage}%.",
            NotificationIcon.Warning,
            sound: soundEnabled);
    }

    private void Render()
    {
        var hasError = _viewModel.HasAuthError;

        var settings = TrayIconSettings.CreateDefault();
        try { settings = _trayIconSettingsStore.Load(); }
        catch (TrayIconSettingsException) { }

        var mainColor = ResolveColor(_viewModel.SessionStatus, settings);

        PaceStatus? pace = null;
        if (settings.ShowPaceMarker)
        {
            var elapsedFraction = Math.Clamp(
                1.0 - (_viewModel.SessionResetTime - DateTimeOffset.Now).TotalHours / 5.0,
                0.0, 1.0);
            pace = PaceStatusCalculator.Calculate(_viewModel.SessionPercentage, elapsedFraction);
            // elapsedFraction is stored for passing to renderers
            _lastElapsedFraction = elapsedFraction;
        }
        else
        {
            _lastElapsedFraction = 0;
        }

        using var bitmap = hasError
            ? RenderErrorBitmap(ResolveColor(UsageStatusLevel.Critical, settings))
            : settings.Style switch
            {
                TrayIconStyle.ProgressBar => RenderProgressBarBitmap(_viewModel.SessionPercentage, _viewModel.IsStale, mainColor, pace, settings.ShowPaceMarker, _lastElapsedFraction),
                TrayIconStyle.Compact     => RenderCompactBitmap(_viewModel.SessionPercentage, _viewModel.IsStale, mainColor, pace),
                _                         => RenderProgressRingBitmap(_viewModel.SessionPercentage, _viewModel.IsStale, mainColor, pace, settings.ShowPaceMarker, _lastElapsedFraction)
            };

        var hIcon = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            _taskbarIcon.Icon = icon;
        }
        finally
        {
            // Shell_NotifyIcon copies the icon synchronously during the setter above,
            // so it's safe to release our handle immediately after.
            DestroyIcon(hIcon);
        }

        if (hasError)
        {
            _taskbarIcon.ToolTipText = "Claude Usage Tracker — session key expired, click to reconnect";
        }
        else if (settings.ShowRemainingPercentage)
        {
            var sRem = 100 - _viewModel.SessionPercentage;
            var wRem = 100 - _viewModel.WeeklyPercentage;
            _taskbarIcon.ToolTipText = $"Claude Usage Tracker — Session {sRem:0}% remaining · Weekly {wRem:0}% remaining";
        }
        else
        {
            _taskbarIcon.ToolTipText = $"Claude Usage Tracker — Session {_viewModel.SessionPercentage:0}% · Weekly {_viewModel.WeeklyPercentage:0}%";
        }
    }

    // --- Progress Ring (default) ---
    // Thick donut-style ring (arc thickness ~ half the ring radius), padded a few px
    // from the edge so round end-caps aren't clipped when Windows scales the 32x32
    // bitmap down to ~16x16 in the tray.
    private const float RingPadding = 3f;
    private const float RingThickness = 6f;
    private static readonly Color TrackColor = Color.FromArgb(90, 255, 255, 255);

    private static Bitmap RenderProgressRingBitmap(double sessionPercentage, bool isStale, Color arcColor, PaceStatus? pace, bool showPaceMarker, double elapsedFraction)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var ringRect = new RectangleF(RingPadding, RingPadding, IconSize - 2 * RingPadding, IconSize - 2 * RingPadding);

        using (var trackPen = new Pen(TrackColor, RingThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawEllipse(trackPen, ringRect);

        var sweepAngle = 360f * (float)Math.Clamp(sessionPercentage / 100.0, 0.0, 1.0);
        if (sweepAngle > 0)
        {
            using var arcPen = new Pen(arcColor, RingThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            // GDI+ angles start at 3 o'clock; rotate start to 12 o'clock (-90) and sweep clockwise.
            g.DrawArc(arcPen, ringRect, -90f, sweepAngle);
        }

        if (showPaceMarker)
        {
            float cx = IconSize / 2f;
            float cy = IconSize / 2f;
            float radius = (IconSize - 2 * RingPadding) / 2f;
            float angleDeg = -90f + (float)(elapsedFraction * 360.0);
            float angleRad = angleDeg * (float)Math.PI / 180f;
            float innerR = radius - 2f;
            float outerR = radius + 2f;
            var innerPt = new PointF(cx + innerR * (float)Math.Cos(angleRad), cy + innerR * (float)Math.Sin(angleRad));
            var outerPt = new PointF(cx + outerR * (float)Math.Cos(angleRad), cy + outerR * (float)Math.Sin(angleRad));
            var tickColor = pace.HasValue ? PaceColor(pace.Value) : Color.White;
            using var tickPen = new Pen(tickColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(tickPen, innerPt, outerPt);
        }

        DrawStaleDot(g, isStale);
        return bitmap;
    }

    // --- Progress Bar ---
    private static Bitmap RenderProgressBarBitmap(double sessionPercentage, bool isStale, Color fillColor, PaceStatus? pace, bool showPaceMarker, double elapsedFraction)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        const float barLeft = 2f, barTop = 13f, barWidth = 28f, barHeight = 6f, corner = 3f;

        // Track
        using (var trackBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            FillRoundedRect(g, trackBrush, barLeft, barTop, barWidth, barHeight, corner);

        // Fill — clip to bar bounds so the rounded fill never overflows the track
        var fillWidth = (float)(barWidth * Math.Clamp(sessionPercentage / 100.0, 0.0, 1.0));
        if (fillWidth > 0)
        {
            g.SetClip(new RectangleF(barLeft, barTop, barWidth, barHeight));
            using (var fillBrush = new SolidBrush(fillColor))
                FillRoundedRect(g, fillBrush, barLeft, barTop, fillWidth, barHeight, corner);
            g.ResetClip();
        }

        if (showPaceMarker)
        {
            float tickX = barLeft + barWidth * (float)Math.Clamp(elapsedFraction, 0.0, 1.0);
            var tickColor = pace.HasValue ? PaceColor(pace.Value) : Color.White;
            using var tickPen = new Pen(tickColor, 2f);
            g.DrawLine(tickPen, tickX, barTop, tickX, barTop + barHeight);
        }

        DrawStaleDot(g, isStale);
        return bitmap;
    }

    // --- Compact ---
    private static Bitmap RenderCompactBitmap(double sessionPercentage, bool isStale, Color dotColor, PaceStatus? pace)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        const float dotSize = 14f;
        const float dotOffset = (IconSize - dotSize) / 2f;
        using (var dotBrush = new SolidBrush(dotColor))
            g.FillEllipse(dotBrush, dotOffset, dotOffset, dotSize, dotSize);

        if (pace.HasValue)
        {
            const float paceDotSize = 4f;
            const float paceDotX = dotOffset + dotSize + 2f;
            float paceDotY = dotOffset + (dotSize - paceDotSize) / 2f;
            using var paceBrush = new SolidBrush(PaceColor(pace.Value));
            g.FillEllipse(paceBrush, paceDotX, paceDotY, paceDotSize, paceDotSize);
        }

        DrawStaleDot(g, isStale);
        return bitmap;
    }

    // --- Error ---
    private static Bitmap RenderErrorBitmap(Color color)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        using (var backgroundBrush = new SolidBrush(color))
            g.FillEllipse(backgroundBrush, 0, 0, IconSize, IconSize);

        using var font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("!", font, textBrush, new RectangleF(0, 0, IconSize, IconSize), format);

        return bitmap;
    }

    // --- Shared helpers ---

    private static void DrawStaleDot(Graphics g, bool isStale)
    {
        if (!isStale)
            return;
        using var staleBrush = new SolidBrush(Color.Gainsboro);
        g.FillEllipse(staleBrush, IconSize - 10, IconSize - 10, 9, 9);
    }

    private static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
    {
        using var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, float x, float y, float width, float height, float radius)
    {
        using var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static Color ResolveColor(UsageStatusLevel level, TrayIconSettings settings) =>
        settings.ColorMode switch
        {
            TrayIconColorMode.Monochrome  => Color.White,
            TrayIconColorMode.SingleColor => ParseHexColor(settings.SingleColorHex),
            _ => level switch
            {
                UsageStatusLevel.Safe     => Color.FromArgb(52, 168, 83),
                UsageStatusLevel.Moderate => Color.FromArgb(251, 140, 0),
                UsageStatusLevel.Critical => Color.FromArgb(217, 48, 37),
                _                         => Color.Gray
            }
        };

    private static Color ParseHexColor(string hex)
    {
        try { return System.Drawing.ColorTranslator.FromHtml(hex); }
        catch { return Color.DeepSkyBlue; }
    }

    private static Color PaceColor(PaceStatus status) => status switch
    {
        PaceStatus.Comfortable => Color.FromArgb(52, 168, 83),
        PaceStatus.OnTrack     => Color.FromArgb(0, 128, 128),
        PaceStatus.Warming     => Color.FromArgb(251, 200, 0),
        PaceStatus.Pressing    => Color.FromArgb(251, 140, 0),
        PaceStatus.Critical    => Color.FromArgb(217, 48, 37),
        PaceStatus.Runaway     => Color.FromArgb(136, 0, 170),
        _                      => Color.Gray
    };

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose() => _taskbarIcon.Dispose();
}
