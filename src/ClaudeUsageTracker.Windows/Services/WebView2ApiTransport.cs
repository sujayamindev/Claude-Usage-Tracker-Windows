using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Routes claude.ai/api requests through a hidden WebView2 (real Chromium) instance instead of
/// HttpClient. Necessary because Cloudflare's bot-management fingerprints .NET's HttpClient TLS
/// handshake and blocks it with a JS challenge — see the 2026-07-01 spec addendum for why.
///
/// Results come back via window.chrome.webview.postMessage / WebMessageReceived rather than
/// ExecuteScriptAsync's return value: in practice ExecuteScriptAsync captured the un-awaited
/// Promise object from our async IIFE (serializing to "{}") instead of its resolved value, so
/// the dedicated host&lt;-&gt;page messaging channel is used instead, correlated by a request ID.
/// </summary>
public sealed class WebView2ApiTransport : IClaudeApiTransport, IDisposable
{
    private const string Origin = "https://claude.ai";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly Window _hostWindow;
    private readonly WebView2 _webView;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private Task? _initializeTask;

    public WebView2ApiTransport()
    {
        _webView = new WebView2();
        _hostWindow = new Window
        {
            Content = _webView,
            Width = 1,
            Height = 1,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            Left = -2000,
            Top = -2000
        };
    }

    /// <summary>Must be called once before the first GetAsync — creates the WebView2 environment
    /// and loads claude.ai so subsequent fetch() calls are same-origin.</summary>
    public Task InitializeAsync() => _initializeTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        _hostWindow.Show();

        // EnsureCoreWebView2Async() with no environment defaults the user-data folder to the
        // executable's own directory. That's writable in Debug (bin/Debug/...) but not once
        // installed to Program Files, where WebView2 then fails with UnauthorizedAccessException
        // trying to create its profile folder. Point it at a per-user writable location instead.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeUsageTracker", "WebView2");
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);

        await _webView.EnsureCoreWebView2Async(environment);
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var navigationCompleted = new TaskCompletionSource();
        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
            navigationCompleted.TrySetResult();

        _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        try
        {
            _webView.CoreWebView2.Navigate(Origin + "/");
            await navigationCompleted.Task;
        }
        finally
        {
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message;
        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch (InvalidOperationException)
        {
            return; // Not a plain string message — not one of ours.
        }

        using var doc = JsonDocument.Parse(message);
        if (doc.RootElement.TryGetProperty("requestId", out var requestIdProp) &&
            requestIdProp.GetString() is { } requestId &&
            _pendingRequests.TryRemove(requestId, out var pending))
        {
            pending.TrySetResult(message);
        }
    }

    public async Task<ApiResponse> GetAsync(string path, string sessionKey, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        var cookieManager = _webView.CoreWebView2.CookieManager;
        var cookie = cookieManager.CreateCookie("sessionKey", sessionKey, "claude.ai", "/");
        cookie.IsHttpOnly = true;
        cookie.IsSecure = true;
        cookieManager.AddOrUpdateCookie(cookie);

        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        var url = Origin + "/api" + path;
        var script = $$"""
            (async () => {
                const requestId = {{JsonSerializer.Serialize(requestId)}};
                try {
                    const response = await fetch({{JsonSerializer.Serialize(url)}}, {
                        credentials: 'include',
                        headers: { 'Accept': 'application/json' }
                    });
                    const body = await response.text();
                    window.chrome.webview.postMessage(JSON.stringify({ requestId, status: response.status, body }));
                } catch (error) {
                    window.chrome.webview.postMessage(JSON.stringify({ requestId, status: 0, body: String(error) }));
                }
            })();
            """;

        await _webView.CoreWebView2.ExecuteScriptAsync(script);

        string message;
        try
        {
            using var timeoutCts = new CancellationTokenSource(RequestTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            message = await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw new ClaudeApiException("Timed out waiting for WebView2 to complete the request");
        }

        using var doc = JsonDocument.Parse(message);
        if (!doc.RootElement.TryGetProperty("status", out var statusProp) ||
            !doc.RootElement.TryGetProperty("body", out var bodyProp))
        {
            throw new ClaudeApiException($"Unexpected WebView2 message, raw: {message}");
        }

        return new ApiResponse(statusProp.GetInt32(), bodyProp.GetString() ?? string.Empty);
    }

    public void Dispose()
    {
        if (_webView.CoreWebView2 is not null)
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;

        _webView.Dispose();
        _hostWindow.Close();
    }
}
