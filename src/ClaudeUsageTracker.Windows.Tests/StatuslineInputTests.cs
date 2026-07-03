using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Tests;

public class StatuslineInputTests
{
    [Fact]
    public void TryParse_ParsesAllFieldsWhenPresent()
    {
        var json = """
        {
            "cwd": "C:\\Users\\test\\my-project",
            "model": { "display_name": "Sonnet 4.5" },
            "context_window_percentage": 12.5
        }
        """;

        var input = StatuslineInput.TryParse(json);

        Assert.NotNull(input);
        Assert.Equal("C:\\Users\\test\\my-project", input!.CurrentDirectory);
        Assert.Equal("Sonnet 4.5", input.ModelDisplayName);
        Assert.Equal(12.5, input.ContextWindowPercentage);
    }

    [Fact]
    public void TryParse_ToleratesMissingModelAndContextFields()
    {
        var json = """{ "cwd": "C:\\Users\\test\\my-project" }""";

        var input = StatuslineInput.TryParse(json);

        Assert.NotNull(input);
        Assert.Equal("C:\\Users\\test\\my-project", input!.CurrentDirectory);
        Assert.Null(input.ModelDisplayName);
        Assert.Null(input.ContextWindowPercentage);
    }

    [Fact]
    public void TryParse_ReturnsNullOnMalformedJson()
    {
        Assert.Null(StatuslineInput.TryParse("{ not valid json"));
    }

    [Fact]
    public void TryParse_ReturnsNullOnNullOrEmptyInput()
    {
        Assert.Null(StatuslineInput.TryParse(null));
        Assert.Null(StatuslineInput.TryParse(string.Empty));
        Assert.Null(StatuslineInput.TryParse("   "));
    }

    [Fact]
    public void TryParse_ReturnsEmptyFieldsWhenJsonIsAnEmptyObject()
    {
        var input = StatuslineInput.TryParse("{}");

        Assert.NotNull(input);
        Assert.Null(input!.CurrentDirectory);
        Assert.Null(input.ModelDisplayName);
        Assert.Null(input.ContextWindowPercentage);
    }
}
