using System.Text.RegularExpressions;

namespace ClaudeUsageTracker.Windows.Services;

public sealed class SessionKeyValidationException(string message) : Exception(message);

/// <summary>
/// Ported from the macOS app's SessionKeyValidator (default/strict configuration only).
/// </summary>
public static partial class SessionKeyValidator
{
    private const string RequiredPrefix = "sk-ant-";
    private const int MinLength = 20;
    private const int MaxLength = 500;

    private static readonly string[] SuspiciousPatterns =
        ["<script", "javascript:", "data:", "vbscript:", "file:"];

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex AllowedCharactersRegex();

    /// <summary>Validates and returns the trimmed session key, or throws SessionKeyValidationException.</summary>
    public static string Validate(string sessionKey)
    {
        var trimmed = sessionKey.Trim();

        if (trimmed.Length == 0)
            throw new SessionKeyValidationException("Session key cannot be empty");

        if (trimmed.Any(char.IsWhiteSpace))
            throw new SessionKeyValidationException("Session key cannot contain whitespace");

        if (trimmed.Length < MinLength)
            throw new SessionKeyValidationException(
                $"Session key too short (minimum: {MinLength}, actual: {trimmed.Length})");

        if (trimmed.Length > MaxLength)
            throw new SessionKeyValidationException(
                $"Session key too long (maximum: {MaxLength}, actual: {trimmed.Length})");

        if (!trimmed.StartsWith(RequiredPrefix, StringComparison.Ordinal))
            throw new SessionKeyValidationException($"Session key must start with '{RequiredPrefix}'");

        PerformSecurityChecks(trimmed);

        if (!AllowedCharactersRegex().IsMatch(trimmed))
            throw new SessionKeyValidationException("Session key contains invalid characters");

        ValidateFormat(trimmed);

        return trimmed;
    }

    public static bool IsValid(string sessionKey)
    {
        try
        {
            Validate(sessionKey);
            return true;
        }
        catch (SessionKeyValidationException)
        {
            return false;
        }
    }

    private static void PerformSecurityChecks(string sessionKey)
    {
        if (sessionKey.Contains('\0'))
            throw new SessionKeyValidationException("Session key rejected for security: contains null bytes");

        if (sessionKey.Any(char.IsControl))
            throw new SessionKeyValidationException("Session key rejected for security: contains control characters");

        if (sessionKey.Contains("..") || sessionKey.Contains("//"))
            throw new SessionKeyValidationException("Session key rejected for security: contains suspicious patterns");

        var lowered = sessionKey.ToLowerInvariant();
        if (SuspiciousPatterns.Any(lowered.Contains))
            throw new SessionKeyValidationException("Session key rejected for security: contains script injection pattern");
    }

    private static void ValidateFormat(string sessionKey)
    {
        var afterPrefix = sessionKey[RequiredPrefix.Length..];

        if (afterPrefix.Length == 0)
            throw new SessionKeyValidationException("Invalid session key format: no content after prefix");

        if (!afterPrefix.Contains('-') && !afterPrefix.Contains('_'))
            throw new SessionKeyValidationException("Invalid session key format: missing expected separators");
    }
}
