using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ClaudeUsageTracker.Windows.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class SetupWindow : FluentWindow
{
    private readonly ClaudeApiClient _apiClient;
    private readonly CliCredentialReader _cliCredentialReader;
    private readonly DispatcherTimer? _cliWatchTimer;
    private DispatcherTimer? _cookiePollTimer;
    private Window? _ssoPopupWindow;

    public StoredCredentials? Result { get; private set; }
    public bool CliLoginDetected { get; private set; }

    public SetupWindow(ClaudeApiClient apiClient, CliCredentialReader cliCredentialReader, bool watchForCliLogin)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _apiClient = apiClient;
        _cliCredentialReader = cliCredentialReader;

        if (watchForCliLogin)
        {
            _cliWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _cliWatchTimer.Tick += (_, _) => CheckForCliLogin();
            _cliWatchTimer.Start();
        }

        Closed += (_, _) =>
        {
            _cliWatchTimer?.Stop();
            _cookiePollTimer?.Stop();
            _ssoPopupWindow?.Close();
            SignInWebView.Dispose();
        };
    }

    private void CheckForCliLogin()
    {
        if (_cliCredentialReader.TryRead() is not { IsExpired: false })
            return;

        _cliWatchTimer!.Stop();
        CliLoginDetected = true;
        DialogResult = true;
        Close();
    }

    private async void SignInWithBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        ManualEntryPanel.Visibility = Visibility.Collapsed;
        BrowserSignInPanel.Visibility = Visibility.Visible;
        SizeToContent = SizeToContent.Manual;
        Height = 640;

        var environment = await WebView2EnvironmentFactory.CreateAsync();
        await SignInWebView.EnsureCoreWebView2Async(environment);
        SignInWebView.CoreWebView2.NewWindowRequested += OnSsoPopupRequested;

        await ClearClaudeCookiesAsync();

        SignInWebView.CoreWebView2.Navigate("https://claude.ai/login");

        _cookiePollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cookiePollTimer.Tick += async (_, _) => await PollForSessionCookieAsync();
        _cookiePollTimer.Start();
    }

    private async Task PollForSessionCookieAsync()
    {
        if (SignInWebView.CoreWebView2 is null)
            return;

        var cookies = await SignInWebView.CoreWebView2.CookieManager.GetCookiesAsync("https://claude.ai");
        var sessionCookie = cookies.FirstOrDefault(c => c.Name == "sessionKey");
        if (sessionCookie is null)
            return;

        _cookiePollTimer?.Stop();
        _cookiePollTimer = null;

        BrowserSignInPanel.Visibility = Visibility.Collapsed;
        ManualEntryPanel.Visibility = Visibility.Visible;
        SizeToContent = SizeToContent.Height;

        await CompleteSignInAsync(sessionCookie.Value);
    }

    private async void OnSsoPopupRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            e.Handled = true;

            var popupWebView = new WebView2();
            _ssoPopupWindow = new FluentWindow
            {
                Title = "Sign In",
                Width = 480,
                Height = 640,
                Content = popupWebView,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var environment = await WebView2EnvironmentFactory.CreateAsync();
            await popupWebView.EnsureCoreWebView2Async(environment);

            popupWebView.CoreWebView2.WindowCloseRequested += (_, _) => _ssoPopupWindow?.Close();
            _ssoPopupWindow.Closed += (_, _) =>
            {
                popupWebView.Dispose();
                _ssoPopupWindow = null;
            };

            e.NewWindow = popupWebView.CoreWebView2;
            _ssoPopupWindow.Show();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task ClearClaudeCookiesAsync()
    {
        var cookieManager = SignInWebView.CoreWebView2.CookieManager;
        foreach (var uri in new[] { "https://claude.ai", "https://anthropic.com" })
        {
            var cookies = await cookieManager.GetCookiesAsync(uri);
            foreach (var cookie in cookies)
                cookieManager.DeleteCookie(cookie);
        }
    }

    private void CancelSignInButton_Click(object sender, RoutedEventArgs e)
    {
        _cookiePollTimer?.Stop();
        _cookiePollTimer = null;
        SignInWebView.CoreWebView2?.Stop();
        BrowserSignInPanel.Visibility = Visibility.Collapsed;
        ManualEntryPanel.Visibility = Visibility.Visible;
        SizeToContent = SizeToContent.Height;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        ConnectButton.IsEnabled = false;

        try
        {
            await CompleteSignInAsync(SessionKeyBox.Text);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private async Task CompleteSignInAsync(string rawSessionKey)
    {
        try
        {
            var sessionKey = SessionKeyValidator.Validate(rawSessionKey);
            var organizations = await _apiClient.FetchOrganizationsAsync(sessionKey);
            var organization = organizations[0];

            Result = new StoredCredentials(sessionKey, organization.Uuid, organization.Name);
            DialogResult = true;
            Close();
        }
        catch (SessionKeyValidationException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (ClaudeApiException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Unexpected error: {ex.Message}";
        }
    }
}
