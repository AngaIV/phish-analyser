using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Rules;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Rules;

public class DisplayNameSpoofRuleTests
{
    private static DisplayNameSpoofRule Rule => new();

    private static MimeMessage Message(string? from)
    {
        var message = new MimeMessage();
        if (from is not null)
            message.Headers.Add("From", from);
        return message;
    }

    private static Finding Only(IReadOnlyList<Finding> results) => Assert.Single(results);

    [Fact]
    public void BrandInDisplayName_OnUnrelatedDomain_IsFlaggedHigh()
    {
        var finding = Only(Rule.Evaluate(Message("\"PayPal\" <service@kplusinternational.com>")));

        Assert.Equal(RuleStatus.Flagged, finding.Status);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(4, finding.Weight);
    }

    [Fact]
    public void BrandInDisplayName_OnTheBrandsDomain_IsPass()
    {
        var finding = Only(Rule.Evaluate(Message("\"PayPal\" <service@paypal.com>")));

        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void BrandInDisplayName_OnABrandSubdomain_IsPass()
    {
        var finding = Only(Rule.Evaluate(Message("\"PayPal\" <service@mail.paypal.com>")));

        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void BrandAliasDomain_IsTreatedAsAligned()
    {
        // Microsoft mail legitimately arrives from office.com.
        var finding = Only(Rule.Evaluate(Message("\"Microsoft\" <no-reply@office.com>")));

        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void LookalikeDomainClaimingABrand_IsStillFlagged()
    {
        var finding = Only(Rule.Evaluate(Message("\"PayPal\" <service@paypal-secure.com>")));

        Assert.Equal(RuleStatus.Flagged, finding.Status);
    }

    [Fact]
    public void BrandAsSubstringOfAWord_IsNotMatched()
    {
        // "Startups" contains "ups" but must not match the UPS brand.
        var finding = Only(Rule.Evaluate(Message("\"Startups Weekly\" <news@startups.example.com>")));

        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void AddressAsDisplayName_OnADifferentDomain_IsFlagged()
    {
        var finding = Only(Rule.Evaluate(Message("\"do_not_reply@irs.gov\" <bounce@evil.example>")));

        Assert.Equal(RuleStatus.Flagged, finding.Status);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(2, finding.Weight);
    }

    [Fact]
    public void AddressAsDisplayName_OnTheSameDomain_IsPass()
    {
        var finding = Only(Rule.Evaluate(Message("\"billing@brand.com\" <billing@brand.com>")));

        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void OrdinaryDisplayName_IsPass()
    {
        var finding = Only(Rule.Evaluate(Message("\"John Smith\" <john@example.com>")));

        Assert.Equal(RuleStatus.Pass, finding.Status);
    }

    [Fact]
    public void NoDisplayName_IsUnknown()
    {
        var finding = Only(Rule.Evaluate(Message("<john@example.com>")));

        Assert.Equal(RuleStatus.Unknown, finding.Status);
    }

    [Fact]
    public void MissingFrom_IsUnknown()
    {
        var finding = Only(Rule.Evaluate(Message(null)));

        Assert.Equal(RuleStatus.Unknown, finding.Status);
    }
}