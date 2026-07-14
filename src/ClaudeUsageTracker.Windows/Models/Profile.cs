namespace ClaudeUsageTracker.Windows.Models;

public enum ProfileAuthMode
{
    SessionKey,
    CliOAuth
}

/// <summary>
/// Non-secret per-profile data. Secrets (session key) live in CredentialStore, namespaced by
/// Id — never on this type. Mirrors the macOS app's split between plist-stored Profile fields
/// and Keychain-stored secrets.
/// </summary>
public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ProfileAuthMode AuthMode { get; set; } = ProfileAuthMode.SessionKey;
    public string? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
}
