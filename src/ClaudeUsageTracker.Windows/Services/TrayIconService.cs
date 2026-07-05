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

        TrayIconStyle style = TrayIconStyle.ProgressRing;
        try { style = _trayIconSettingsStore.Load().Style; }
        catch (TrayIconSettingsException) { }

        using var bitmap = hasError
            ? RenderErrorBitmap(StatusColor(UsageStatusLevel.Critical))
            : style switch
            {
                TrayIconStyle.ProgressBar => RenderProgressBarBitmap(_viewModel.SessionPercentage, _viewModel.IsStale, StatusColor(_viewModel.SessionStatus)),
                TrayIconStyle.Compact     => RenderCompactBitmap(_viewModel.SessionPercentage, _viewModel.IsStale, StatusColor(_viewModel.SessionStatus)),
                _                         => RenderProgressRingBitmap(_viewModel.SessionPercentage, _viewModel.IsStale, StatusColor(_viewModel.SessionStatus))
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

        _taskbarIcon.ToolTipText = hasError
            ? "Claude Usage Tracker — session key expired, click to reconnect"
            : $"Claude Usage Tracker — Session {_viewModel.SessionPercentage:0}% · Weekly {_viewModel.WeeklyPercentage:0}%";
    }

    // --- Progress Ring (default) ---
    // Thick donut-style ring (arc thickness ~ half the ring radius), padded a few px
    // from the edge so round end-caps aren't clipped when Windows scales the 32x32
    // bitmap down to ~16x16 in the tray.
    private const float RingPadding = 3f;
    private const float RingThickness = 6f;
    private static readonly Color TrackColor = Color.FromArgb(90, 255, 255, 255);

    private static Bitmap RenderProgressRingBitmap(double sessionPercentage, bool isStale, Color arcColor)
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

        DrawStaleDot(g, isStale);
        return bitmap;
    }

    // --- Progress Bar ---
    private static Bitmap RenderProgressBarBitmap(double sessionPercentage, bool isStale, Color fillColor)
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

        DrawStaleDot(g, isStale);
        return bitmap;
    }

    // --- Compact ---
    private static Bitmap RenderCompactBitmap(double sessionPercentage, bool isStale, Color dotColor)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        const float dotSize = 14f;
        const float dotOffset = (IconSize - dotSize) / 2f;
        using (var dotBrush = new SolidBrush(dotColor))
            g.FillEllipse(dotBrush, dotOffset, dotOffset, dotSize, dotSize);

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

    private static Color StatusColor(UsageStatusLevel level) => level switch
    {
        UsageStatusLevel.Safe => Color.FromArgb(52, 168, 83),
        UsageStatusLevel.Moderate => Color.FromArgb(251, 140, 0),
        UsageStatusLevel.Critical => Color.FromArgb(217, 48, 37),
        _ => Color.Gray
    };

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose() => _taskbarIcon.Dispose();
}
