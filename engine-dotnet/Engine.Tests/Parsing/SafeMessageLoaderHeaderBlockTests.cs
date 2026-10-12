using PhishAnalyser.Engine.Parsing;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Parsing;

/// <summary>
/// Covers the in-memory overload used when headers come from a mail client rather than a
/// file. The guarantees must match the file path exactly.
/// </summary>
public class SafeMessageLoaderHeaderBlockTests
{
    private const string ValidBlock =
        "From: PayPal <service@paypal-secure.com>\r\n" +
        "To: victim@example.com\r\n" +
        "Subject: Verify your account\r\n" +
        "Received: from unknown (HELO mail.example.com) by mx.example.com\r\n";

    [Fact]
    public void LoadFromHeaderBlock_ValidBlock_Succeeds()
    {
        var result = SafeMessageLoader.LoadFromHeaderBlock(ValidBlock);

        Assert.True(result.Succeeded);
        Assert.Equal("service@paypal-secure.com", result.Message!.From.Mailboxes.Single().Address);
        Assert.Equal("PayPal", result.Message.From.Mailboxes.Single().Name);
    }

    [Fact]
    public void LoadFromHeaderBlock_WithoutTrailingBlankLine_StillParses()
    {
        // The normalisation must not depend on the caller supplying the terminator.
        var result = SafeMessageLoader.LoadFromHeaderBlock(ValidBlock.TrimEnd('\r', '\n'));

        Assert.True(result.Succeeded);
        Assert.Equal("Verify your account", result.Message!.Subject);
    }

    [Fact]
    public void LoadFromHeaderBlock_ParsesHeadersIdenticallyToTheFileOverload()
    {
        var fromBlock = SafeMessageLoader.LoadFromHeaderBlock(ValidBlock);

        var path = Path.Combine(Path.GetTempPath(), $"phish-analyser-{Guid.NewGuid():N}.eml");
        try
        {
            File.WriteAllText(path, ValidBlock);
            var fromFile = SafeMessageLoader.Load(path);

            Assert.True(fromBlock.Succeeded);
            Assert.True(fromFile.Succeeded);
            Assert.Equal(fromFile.Message!.From.ToString(), fromBlock.Message!.From.ToString());
            Assert.Equal(fromFile.Message.Subject, fromBlock.Message.Subject);
            Assert.Equal(fromFile.Warnings, fromBlock.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromHeaderBlock_Empty_IsUnknownNotPass()
    {
        // Fail-safe: nothing to check is never reported as "nothing found".
        foreach (var input in new[] { "", "   ", "\r\n\r\n" })
        {
            var result = SafeMessageLoader.LoadFromHeaderBlock(input);
            Assert.False(result.Succeeded);
            Assert.NotEmpty(result.Warnings);
        }
    }

    [Fact]
    public void LoadFromHeaderBlock_Null_DoesNotThrow()
    {
        var result = SafeMessageLoader.LoadFromHeaderBlock(null!);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void LoadFromHeaderBlock_Oversized_IsRejectedBeforeParsing()
    {
        var padding = new string('x', (int)SafeMessageLoader.MaxBytes + 1);
        var result = SafeMessageLoader.LoadFromHeaderBlock($"Subject: {padding}\r\n");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Warnings, w => w.Contains("byte limit", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadFromHeaderBlock_Garbage_DoesNotThrow()
    {
        var result = SafeMessageLoader.LoadFromHeaderBlock("this is not\0a header block\n\x01\x02");

        // Whatever the outcome, the contract is that no exception escapes.
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public void LoadFromHeaderBlock_ReportsMissingFromAsWarning()
    {
        var result = SafeMessageLoader.LoadFromHeaderBlock("Subject: no sender here\r\n");

        Assert.True(result.Succeeded);
        Assert.Contains(result.Warnings, w => w.Contains("missing From", StringComparison.Ordinal));
    }
}