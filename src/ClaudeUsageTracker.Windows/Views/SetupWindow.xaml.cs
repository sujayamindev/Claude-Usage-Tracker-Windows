using System.Windows;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class SetupWindow : FluentWindow
{
    private readonly ClaudeApiClient _apiClient;

    public StoredCredentials? Result { get; private set; }

    public SetupWindow(ClaudeApiClient apiClient)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _apiClient = apiClient;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        ConnectButton.IsEnabled = false;

        try
        {
            var sessionKey = SessionKeyValidator.Validate(SessionKeyBox.Text);
            var organizations = await _apiClient.FetchOrganizationsAsync(sessionKey);
            var organization = organizations[0];

            var credentials = new StoredCredentials(sessionKey, organization.Uuid, organization.Name);
            CredentialStore.Save(credentials);

            Result = credentials;
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
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }
}
