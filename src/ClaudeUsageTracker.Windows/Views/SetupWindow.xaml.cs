using System.Windows;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Views;

public partial class SetupWindow : Window
{
    private readonly ClaudeApiClient _apiClient;

    public StoredCredentials? Result { get; private set; }

    public SetupWindow(ClaudeApiClient apiClient)
    {
        InitializeComponent();
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

            var credentials = new StoredCredentials(sessionKey, organization.Uuid);
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
