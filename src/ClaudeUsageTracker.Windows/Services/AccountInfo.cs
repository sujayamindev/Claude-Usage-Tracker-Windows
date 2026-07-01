using System.Text.Json.Serialization;

namespace ClaudeUsageTracker.Windows.Services;

public sealed class AccountInfo
{
    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
