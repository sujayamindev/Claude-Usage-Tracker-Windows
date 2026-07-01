using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.ViewModels;
using H.NotifyIcon;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Owns the tray icon, rendering the live session percentage as text on the icon itself
/// (matching the macOS menu bar behavior), ported from MenuBarManager/MenuBarIconRenderer.
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

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Claude Usage Tracker",
            ContextMenu = new ContextMenu { Items = { exitItem } }
        };
        _taskbarIcon.TrayLeftMouseUp += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);

        _viewModel.PropertyChanged += (_, _) => Render();
        Render();
    }

    private void Render()
    {
        var hasError = _viewModel.HasAuthError;
        var text = hasError ? "!" : $"{Math.Round(_viewModel.SessionPercentage):0}";
        var status = hasError ? UsageStatusLevel.Critical : _viewModel.SessionStatus;

        using var bitmap = RenderIconBitmap(text, StatusColor(status), _viewModel.IsStale && !hasError);
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

    private static Bitmap RenderIconBitmap(string text, Color color, bool isStale)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        using (var backgroundBrush = new SolidBrush(color))
            g.FillEllipse(backgroundBrush, 0, 0, IconSize, IconSize);

        var fontSize = text.Length >= 3 ? 10f : 13f;
        using var font = new Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, new RectangleF(0, 0, IconSize, IconSize), format);

        if (isStale)
        {
            using var staleBrush = new SolidBrush(Color.Gainsboro);
            g.FillEllipse(staleBrush, IconSize - 10, IconSize - 10, 9, 9);
        }

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
