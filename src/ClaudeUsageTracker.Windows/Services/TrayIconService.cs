using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.ViewModels;
using H.NotifyIcon;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Owns the tray icon, rendering the live session percentage as a circular progress ring
/// on the icon itself (ported from the macOS app's `.icon` style in MenuBarIconRenderer).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int IconSize = 32;

    private readonly TaskbarIcon _taskbarIcon;
    private readonly UsageViewModel _viewModel;

    public event EventHandler? Clicked;
    public event EventHandler? ExitRequested;

    public TrayIconService(UsageViewModel viewModel)
    {
        _viewModel = viewModel;

        var startupItem = new MenuItem { Header = "Launch at Startup", IsCheckable = true, IsChecked = StartupService.IsEnabled() };
        startupItem.Click += (_, _) => StartupService.SetEnabled(startupItem.IsChecked);

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Claude Usage Tracker",
            ContextMenu = new ContextMenu { Items = { startupItem, new Separator(), exitItem } }
        };
        _taskbarIcon.TrayLeftMouseUp += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);

        // Required when the TaskbarIcon isn't placed in a loaded visual tree/resources (our case,
        // since it's constructed directly in code) — otherwise the tray icon is never actually created.
        _taskbarIcon.ForceCreate();

        _viewModel.PropertyChanged += (_, _) => Render();
        Render();
    }

    private void Render()
    {
        var hasError = _viewModel.HasAuthError;

        using var bitmap = hasError
            ? RenderErrorBitmap(StatusColor(UsageStatusLevel.Critical))
            : RenderProgressRingBitmap(_viewModel.SessionPercentage, _viewModel.IsStale);
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

    // Thick donut-style ring (arc thickness ~ half the ring radius), padded a few px
    // from the edge so round end-caps aren't clipped when Windows scales the 32x32
    // bitmap down to ~16x16 in the tray.
    private const float RingPadding = 3f;
    private const float RingThickness = 6f;

    private static readonly Color TrackColor = Color.FromArgb(90, 255, 255, 255);

    private static Bitmap RenderProgressRingBitmap(double sessionPercentage, bool isStale)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var ringRect = new RectangleF(RingPadding, RingPadding, IconSize - 2 * RingPadding, IconSize - 2 * RingPadding);

        using (var trackPen = new Pen(TrackColor, RingThickness) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            g.DrawEllipse(trackPen, ringRect);

        var sweepAngle = 360f * (float)Math.Clamp(sessionPercentage / 100.0, 0.0, 1.0);
        if (sweepAngle > 0)
        {
            using var arcPen = new Pen(Color.White, RingThickness) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            // GDI+ angles start at 3 o'clock; rotate start to 12 o'clock (-90) and sweep clockwise.
            g.DrawArc(arcPen, ringRect, -90f, sweepAngle);
        }

        if (isStale)
        {
            using var staleBrush = new SolidBrush(Color.Gainsboro);
            g.FillEllipse(staleBrush, IconSize - 10, IconSize - 10, 9, 9);
        }

        return bitmap;
    }

    private static Bitmap RenderErrorBitmap(Color color)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        using (var backgroundBrush = new SolidBrush(color))
            g.FillEllipse(backgroundBrush, 0, 0, IconSize, IconSize);

        using var font = new Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("!", font, textBrush, new RectangleF(0, 0, IconSize, IconSize), format);

        return bitmap;
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
