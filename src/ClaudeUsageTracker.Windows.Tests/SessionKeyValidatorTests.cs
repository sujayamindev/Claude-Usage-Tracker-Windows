using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class SessionKeyValidatorTests
{
    [Theory]
    [InlineData("sk-ant-sid01-abcdefghijklmnopqrstuvwxyz1234567890")]
    [InlineData("sk-ant-sid01_abcdefghijklmnopqrstuvwxyz_1234567890")]
    [InlineData("sk-ant-sid01-abcd-efgh-ijkl-mnop-qrst")]
    public void Validate_AcceptsWellFormedKeys(string key)
    {
        Assert.True(SessionKeyValidator.IsValid(key));
        SessionKeyValidator.Validate(key);
    }

    [Fact]
    public void Validate_TrimsLeadingAndTrailingWhitespace()
    {
        const string keyWithWhitespace = "  sk-ant-sid01-abcdefghijklmnopqrstuvwxyz  ";

        var sanitized = SessionKeyValidator.Validate(keyWithWhitespace);

        Assert.False(sanitized.StartsWith(' '));
        Assert.False(sanitized.EndsWith(' '));
    }

    [Fact]
    public void Validate_ThrowsOnEmpty()
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate(""));
    }

    [Fact]
    public void Validate_ThrowsOnWhitespaceOnly()
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("   "));
    }

    [Theory]
    [InlineData("invalid-prefix-abcdefghijklmnopqrstuvwxyz")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567890")]
    public void Validate_ThrowsOnMissingOrInvalidPrefix(string key)
    {
        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate(key));
        Assert.Contains("must start with", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnTooShort()
    {
        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-abc"));
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnTooLong()
    {
        var longKey = "sk-ant-" + new string('a', 1000);

        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate(longKey));
        Assert.Contains("too long", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnInvalidCharacters()
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-sid01-hello@world!#$%"));
    }

    [Fact]
    public void Validate_ThrowsOnInternalWhitespace()
    {
        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-sid01 abcd efgh"));
        Assert.Contains("whitespace", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnNullBytes()
    {
        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-sid01-abc\0def"));
        Assert.Contains("security", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnPathTraversal()
    {
        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-sid01-../etc/passwd"));
        Assert.Contains("security", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnScriptInjection()
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-sid01-<script>alert('xss')</script>"));
    }

    [Fact]
    public void Validate_ThrowsWhenMissingSeparatorAfterPrefix()
    {
        var ex = Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-abcdefghijklmnopqrstuvwxyz"));
        Assert.Contains("format", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnMixedCasePrefix()
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("SK-ANT-sid01-abcdefghijklmnopqrstuvwxyz"));
    }

    [Fact]
    public void Validate_ThrowsOnUnicodeCharacters()
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate("sk-ant-sid01-héllo-wörld"));
    }

    [Theory]
    [InlineData("sk-ant")]
    [InlineData("sk ant sid01 abcd")]
    [InlineData("sessionKey=sk-ant-sid01-abcd")]
    public void Validate_ThrowsOnTypicalUserMistakes(string mistake)
    {
        Assert.Throws<SessionKeyValidationException>(() => SessionKeyValidator.Validate(mistake));
        Assert.False(SessionKeyValidator.IsValid(mistake));
    }
}
