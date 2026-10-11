using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Rules;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Rules;

public class IdentityMismatchRuleTests
{
    private static IdentityMismatchRule Rule => new();

    private static MimeMessage Message(string from, string? returnPath = null, string? replyTo = null)
    {
        var message = new MimeMessage();
        message.Headers.Add("From", from);
        if (returnPath is not null) message.Headers.Add("Return-Path", returnPath);
        if (replyTo is not null) message.Headers.Add("Reply-To", replyTo);
        return message;
    }

    private static Finding For(IReadOnlyList<Finding> results, string fragment) =>
        Assert.Single(results, f => f.Explanation.Contains(fragment));

    [Fact]
    public void AlignedDomains_ProduceNoFlags()
    {
        var results = Rule.Evaluate(Message(
            "Brand <support@brand.com>", "<bounce@brand.com>", "help@brand.com"));

        Assert.DoesNotContain(results, f => f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void ReplyToDifferentDomain_IsFlaggedMedium()
    {
        var results = Rule.Evaluate(Message(
            "Brand <support@brand.com>", "<bounce@brand.com>", "attacker@evil.ru"));

        var finding = For(results, "different company");
        Assert.Equal(RuleStatus.Flagged, finding.Status);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(2, finding.Weight);
    }

    [Fact]
    public void ReturnPathDifferentDomain_IsFlaggedLow()
    {
        var results = Rule.Evaluate(Message(
            "Brand <support@brand.com>", "<bounce@sendgrid.net>", "support@brand.com"));

        var finding = For(results, "receive bounces");
        Assert.Equal(RuleStatus.Flagged, finding.Status);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(1, finding.Weight);
    }

    [Fact]
    public void SubdomainDifference_IsNotFlagged()
    {
        var results = Rule.Evaluate(Message(
            "Brand <support@mail.brand.com>", "<bounce@brand.com>", "help@eu.brand.com"));

        Assert.DoesNotContain(results, f => f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void TwoLetterTldDomains_AreComparedAtRegistrableLevel()
    {
        var same = Rule.Evaluate(Message(
            "Bank <support@nedbank.co.za>", "<bounce@nedbank.co.za>", "help@mail.nedbank.co.za"));
        Assert.DoesNotContain(same, f => f.Status == RuleStatus.Flagged);

        var different = Rule.Evaluate(Message(
            "Bank <support@nedbank.co.za>", "<bounce@nedbank.co.za>", "help@nedbank.co.za.evil.com"));
        Assert.Contains(different, f => f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void MissingReplyTo_IsAPassNotAnUnknown()
    {
        var results = Rule.Evaluate(Message("Brand <support@brand.com>", "<bounce@brand.com>"));

        var finding = For(results, "No reply address");
        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void MissingReturnPath_IsUnknown()
    {
        var results = Rule.Evaluate(Message("Brand <support@brand.com>"));

        var finding = For(results, "no bounce address");
        Assert.Equal(RuleStatus.Unknown, finding.Status);
    }

    [Fact]
    public void MissingFrom_IsUnknownAndShortCircuits()
    {
        var results = Rule.Evaluate(new MimeMessage());

        Assert.Single(results);
        Assert.Equal(RuleStatus.Unknown, results[0].Status);
    }

        [Fact]
    public void ReplyToMismatchOnMailingList_IsNotFlagged()
    {
        var message = Message("Brand <support@brand.com>", "<bounce@brand.com>", "list@lists.example.org");
        message.Headers.Add("List-Id", "Example List <list.example.org>");

        var results = Rule.Evaluate(message);

        Assert.DoesNotContain(results, f => f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void ReturnPathMismatchOnBulkMail_IsNotFlagged()
    {
        var message = Message("Brand <support@brand.com>", "<bounce@sendgrid.net>");
        message.Headers.Add("Precedence", "bulk");

        var results = Rule.Evaluate(message);

        Assert.DoesNotContain(results, f => f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void ListUnsubscribeAlone_SuppressesTheMismatch()
    {
        var message = Message("Brand <support@brand.com>", "<bounce@esp.net>");
        message.Headers.Add("List-Unsubscribe", "<mailto:unsub@esp.net>");

        var results = Rule.Evaluate(message);

        Assert.DoesNotContain(results, f => f.Status == RuleStatus.Flagged);
    }

    [Fact]
    public void MismatchWithoutListHeaders_IsStillFlagged()
    {
        var results = Rule.Evaluate(Message("Brand <support@brand.com>", "<bounce@esp.net>", "x@evil.ru"));

        Assert.Contains(results, f => f.Status == RuleStatus.Flagged);
    }
}