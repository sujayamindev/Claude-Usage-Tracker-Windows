// src/ClaudeUsageTracker.Windows/Views/ManageProfilesWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClaudeUsageTracker.Windows.Views;

public partial class ManageProfilesWindow : FluentWindow
{
    private readonly ProfileManager _profileManager;
    private readonly ClaudeApiClient _apiClient;
    private readonly CliCredentialReader _cliCredentialReader;
    private Guid? _renamingProfileId;

    public ManageProfilesWindow(ProfileManager profileManager, ClaudeApiClient apiClient, CliCredentialReader cliCredentialReader)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        _profileManager = profileManager;
        _apiClient = apiClient;
        _cliCredentialReader = cliCredentialReader;
        _profileManager.ProfilesChanged += (_, _) => Render();
        _profileManager.ActiveProfileChanged += (_, _) => Render();
        Render();
    }

    private void Render()
    {
        ErrorText.Text = string.Empty;
        ProfilesPanel.Children.Clear();

        foreach (var profile in _profileManager.Profiles)
            ProfilesPanel.Children.Add(_renamingProfileId == profile.Id ? BuildRenameRow(profile) : BuildProfileRow(profile));
    }

    private UIElement BuildProfileRow(Profile profile)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var isActive = profile.Id == _profileManager.ActiveProfile.Id;
        var subtitle = profile.AuthMode == ProfileAuthMode.CliOAuth
            ? "Claude Code CLI"
            : profile.OrganizationName ?? "Not signed in";

        var info = new StackPanel();
        info.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = profile.Name + (isActive ? "  (active)" : ""),
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal
        });
        info.Children.Add(new System.Windows.Controls.TextBlock { Text = subtitle, FontSize = 10.5, Foreground = Brushes.Gray });
        Grid.SetColumn(info, 0);
        row.Children.Add(info);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };

        if (!isActive)
        {
            var activateButton = new Wpf.Ui.Controls.Button { Content = "Activate", Margin = new Thickness(0, 0, 6, 0) };
            activateButton.Click += (_, _) => _profileManager.SwitchTo(profile.Id);
            buttons.Children.Add(activateButton);
        }

        var renameButton = new Wpf.Ui.Controls.Button { Content = "Rename", Margin = new Thickness(0, 0, 6, 0) };
        renameButton.Click += (_, _) => { _renamingProfileId = profile.Id; Render(); };
        buttons.Children.Add(renameButton);

        var deleteButton = new Wpf.Ui.Controls.Button { Content = "Delete", IsEnabled = _profileManager.Profiles.Count > 1 };
        deleteButton.Click += (_, _) => DeleteProfile(profile);
        buttons.Children.Add(deleteButton);

        Grid.SetColumn(buttons, 1);
        row.Children.Add(buttons);
        return row;
    }

    private UIElement BuildRenameRow(Profile profile)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBox = new System.Windows.Controls.TextBox { Text = profile.Name, VerticalContentAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nameBox, 0);
        row.Children.Add(nameBox);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };

        var saveButton = new Wpf.Ui.Controls.Button { Content = "Save", Margin = new Thickness(6, 0, 6, 0) };
        saveButton.Click += (_, _) =>
        {
            var newName = nameBox.Text.Trim();
            _renamingProfileId = null;
            if (!string.IsNullOrEmpty(newName))
                _profileManager.RenameProfile(profile.Id, newName);
            else
                Render();
        };
        buttons.Children.Add(saveButton);

        var cancelButton = new Wpf.Ui.Controls.Button { Content = "Cancel" };
        cancelButton.Click += (_, _) => { _renamingProfileId = null; Render(); };
        buttons.Children.Add(cancelButton);

        Grid.SetColumn(buttons, 1);
        row.Children.Add(buttons);
        return row;
    }

    private void DeleteProfile(Profile profile)
    {
        var confirm = System.Windows.MessageBox.Show(
            $"Delete profile \"{profile.Name}\"? This can't be undone.",
            "Delete Profile", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            _profileManager.DeleteProfile(profile.Id);
        }
        catch (ProfileException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var setupWindow = new SetupWindow(_apiClient, _cliCredentialReader, watchForCliLogin: false);
        if (setupWindow.ShowDialog() != true)
            return;

        if (setupWindow.CliLoginDetected)
        {
            _profileManager.CreateProfile("Claude Code Account", ProfileAuthMode.CliOAuth, null);
        }
        else if (setupWindow.Result is not null)
        {
            _profileManager.CreateProfile(setupWindow.Result.OrganizationName, ProfileAuthMode.SessionKey, setupWindow.Result);
        }
    }
}
