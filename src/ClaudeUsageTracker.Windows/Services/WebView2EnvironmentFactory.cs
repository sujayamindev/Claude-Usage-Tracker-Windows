using System.IO;
using Microsoft.Web.WebView2.Core;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Single source of truth for the CoreWebView2Environment user-data folder, shared by every
/// WebView2 instance in the app (WebView2ApiTransport's hidden API-request view, and the
/// embedded browser sign-in view in SetupWindow) so they use the same Chromium profile/cookie
/// jar instead of each spinning up a separate one.
/// </summary>
public static class WebView2EnvironmentFactory
{
    public static Task<CoreWebView2Environment> CreateAsync()
    {
        // EnsureCoreWebView2Async() with no environment defaults the user-data folder to the
        // executable's own directory. That's writable in Debug (bin/Debug/...) but not once
        // installed to Program Files, where WebView2 then fails with UnauthorizedAccessException
        // trying to create its profile folder. Point it at a per-user writable location instead.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeUsageTracker", "WebView2");
        return CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
    }
}
