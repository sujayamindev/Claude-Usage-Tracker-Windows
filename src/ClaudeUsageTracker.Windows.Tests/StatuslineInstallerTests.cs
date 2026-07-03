using System.Text.Json.Nodes;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class StatuslineInstallerTests
{
    private const string FakeExePath = "C:\\fake\\ClaudeUsageTracker.Windows.exe";
    private static string ExpectedCommand => $"\"{FakeExePath}\" --statusline";

    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"claude-settings-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Enable_CreatesFileWithStatusLineKeyWhenFileDoesNotExist()
    {
        var path = TempSettingsPath();
        try
        {
            new StatuslineInstaller(path, FakeExePath).Enable();

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Equal("command", root["statusLine"]!["type"]!.GetValue<string>());
            Assert.Equal(ExpectedCommand, root["statusLine"]!["command"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Enable_PreservesUnrelatedExistingKeys()
    {
        var path = TempSettingsPath();
        File.WriteAllText(path, """{ "permissions": { "allow": ["Bash"] } }""");
        try
        {
            new StatuslineInstaller(path, FakeExePath).Enable();

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Equal("Bash", root["permissions"]!["allow"]![0]!.GetValue<string>());
            Assert.Equal(ExpectedCommand, root["statusLine"]!["command"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Disable_RemovesStatusLineKeyButKeepsOtherKeys()
    {
        var path = TempSettingsPath();
        File.WriteAllText(path, $$"""
        {
            "permissions": { "allow": ["Bash"] },
            "statusLine": { "type": "command", "command": "{{ExpectedCommand.Replace("\\", "\\\\").Replace("\"", "\\\"")}}" }
        }
        """);
        try
        {
            new StatuslineInstaller(path, FakeExePath).Disable();

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.False(root.ContainsKey("statusLine"));
            Assert.Equal("Bash", root["permissions"]!["allow"]![0]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Disable_IsNoOpWhenFileDoesNotExist()
    {
        var path = TempSettingsPath();

        var exception = Record.Exception(() => new StatuslineInstaller(path, FakeExePath).Disable());

        Assert.Null(exception);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void IsEnabled_ReturnsFalseWhenFileDoesNotExist()
    {
        var installer = new StatuslineInstaller(TempSettingsPath(), FakeExePath);

        Assert.False(installer.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsTrueAfterEnable()
    {
        var path = TempSettingsPath();
        try
        {
            var installer = new StatuslineInstaller(path, FakeExePath);
            installer.Enable();

            Assert.True(installer.IsEnabled());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IsEnabled_ReturnsFalseAfterDisable()
    {
        var path = TempSettingsPath();
        try
        {
            var installer = new StatuslineInstaller(path, FakeExePath);
            installer.Enable();
            installer.Disable();

            Assert.False(installer.IsEnabled());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Enable_ThrowsOnCorruptExistingFile()
    {
        var path = TempSettingsPath();
        File.WriteAllText(path, "{ not valid json");
        try
        {
            Assert.Throws<StatuslineSettingsException>(() => new StatuslineInstaller(path, FakeExePath).Enable());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IsEnabled_ThrowsOnCorruptExistingFile()
    {
        var path = TempSettingsPath();
        File.WriteAllText(path, "{ not valid json");
        try
        {
            Assert.Throws<StatuslineSettingsException>(() => new StatuslineInstaller(path, FakeExePath).IsEnabled());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
