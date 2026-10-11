using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Rules;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Rules;

public class AuthResultsRuleTests
{
    private static AuthResultsRule Rule => new();

    private static MimeMessage MessageWithHeader(string name, string value)
    {
        var message = new MimeMessage();
        message.Headers.Add(name, value);
        return message;
    }

    private static MimeMessage MessageWithHeaders(params string[] headerValues)
    {
        var message = new MimeMessage();
        foreach (var value in headerValues)
            message.Headers.Add("Authentication-Results", value);
        return message;
    }

    [Fact]
    public void AllPass_ReturnsNoFlaggedFindings()
    {
        var message = MessageWithHeader("Authentication-Results",
            "mx.example.com; spf=pass smtp.mailfrom=example.com; dkim=pass header.d=example.com; dmarc=pass");

        var results = Rule.Evaluate(message);

        Assert.DoesNotContain(results, f => f.Status == RuleStatus.Flagged);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void SpfFail_ReturnsExactlyOneFlaggedFinding()
    {
        var message = MessageWithHeader("Authentication-Results",
            "mx.example.com; spf=fail smtp.mailfrom=example.com; dkim=pass header.d=example.com; dmarc=pass");

        var results = Rule.Evaluate(message);

        var spf = Assert.Single(results, f => f.RuleId == "auth-results" && f.Explanation.StartsWith("SPF"));
        Assert.Equal(RuleStatus.Flagged, spf.Status);
        Assert.Equal(Severity.Medium, spf.Severity);
    }

    [Fact]
    public void DkimNone_IsUnknownNotFlagged()
    {
        var message = MessageWithHeader("Authentication-Results",
            "mx.example.com; spf=pass smtp.mailfrom=example.com; dkim=none; dmarc=pass");

        var results = Rule.Evaluate(message);

        var dkim = Assert.Single(results, f => f.Explanation.StartsWith("DKIM"));
        Assert.Equal(RuleStatus.Unknown, dkim.Status);
        Assert.Equal(RuleStatus.Pass, Assert.Single(results, f => f.Explanation.StartsWith("SPF")).Status);
    }

    [Fact]
    public void MissingHeader_ReturnsUnknownAcrossTheBoard()
    {
        var results = Rule.Evaluate(new MimeMessage());

        Assert.All(results, f => Assert.Equal(RuleStatus.Unknown, f.Status));
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void UppercaseValues_StillDetected()
    {
        var message = MessageWithHeader("Authentication-Results",
            "mx.example.com; SPF=FAIL smtp.mailfrom=example.com; dkim=pass header.d=example.com; dmarc=pass");

        var results = Rule.Evaluate(message);

        var spf = Assert.Single(results, f => f.Explanation.StartsWith("SPF"));
        Assert.Equal(RuleStatus.Flagged, spf.Status);
    }

    [Fact]
    public void FoldedHeader_SpreadAcrossLines_StillParsed()
    {
        var message = MessageWithHeader("Authentication-Results",
            "mx.example.com;\n\tspf=fail smtp.mailfrom=example.com;\n\tdkim=pass header.d=example.com;\n\tdmarc=pass");

        var results = Rule.Evaluate(message);

        Assert.Single(results, f => f.Explanation.StartsWith("SPF") && f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void TwoAuthResultsHeaders_FirstWins()
    {
        var message = MessageWithHeaders(
            "mx.example.com; spf=fail smtp.mailfrom=example.com; dkim=pass header.d=example.com; dmarc=pass",
            "fake.example.com; spf=pass smtp.mailfrom=example.com");

        var results = Rule.Evaluate(message);

        var spf = Assert.Single(results, f => f.Explanation.StartsWith("SPF"));
        Assert.Equal(RuleStatus.Flagged, spf.Status);
    }
}