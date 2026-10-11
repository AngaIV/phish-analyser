using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Parsing;

namespace PhishAnalyser.Engine.Rules;

public sealed class IdentityMismatchRule : IRule
{
    public string RuleId => "identity-mismatch";

    public IReadOnlyList<Finding> Evaluate(MimeMessage message)
    {
        var findings = new List<Finding>();

        var fromDomain = DomainHelper.RegistrableDomain(FirstAddress(message.From));
        if (fromDomain is null)
        {
            findings.Add(new Finding(RuleId, RuleStatus.Unknown, Severity.Info, 0,
                "The sender's address could not be read, so it could not be checked."));
            return findings;
        }

        var bulkOrList = MessageContext.IsBulkOrList(message);

        findings.Add(EvaluateReturnPath(message, fromDomain, bulkOrList));
        findings.Add(EvaluateReplyTo(message, fromDomain, bulkOrList));
        return findings;
    }

    private Finding EvaluateReturnPath(MimeMessage message, string fromDomain, bool bulkOrList)
    {
        var domain = DomainHelper.RegistrableDomain(message.Headers["Return-Path"]);

        if (domain is null)
            return new Finding(RuleId, RuleStatus.Unknown, Severity.Info, 0,
                "This message has no bounce address, so it could not be checked.");

        if (domain.Equals(fromDomain, StringComparison.OrdinalIgnoreCase))
            return new Finding(RuleId, RuleStatus.Pass, Severity.Info, 0,
                $"The bounce address is on the same domain as the sender ({fromDomain}).");

        if (bulkOrList)
            return new Finding(RuleId, RuleStatus.Pass, Severity.Info, 0,
                $"The bounce address is on {domain}, which is normal for mailing-list and bulk mail.");

        return new Finding(RuleId, RuleStatus.Flagged, Severity.Low, 1,
            $"The address that would receive bounces for this message is on {domain}, not {fromDomain}.");
    }

    private Finding EvaluateReplyTo(MimeMessage message, string fromDomain, bool bulkOrList)
    {
        var replyTo = FirstAddress(message.ReplyTo);
        if (replyTo is null)
            return new Finding(RuleId, RuleStatus.Pass, Severity.Info, 0,
                "No reply address is set, so replies go to the sender.");

        var domain = DomainHelper.RegistrableDomain(replyTo);
        if (domain is null)
            return new Finding(RuleId, RuleStatus.Unknown, Severity.Info, 0,
                "The reply address could not be read, so it could not be checked.");

        if (domain.Equals(fromDomain, StringComparison.OrdinalIgnoreCase))
            return new Finding(RuleId, RuleStatus.Pass, Severity.Info, 0,
                $"Replies would go to the same domain as the sender ({fromDomain}).");

        if (bulkOrList)
            return new Finding(RuleId, RuleStatus.Pass, Severity.Info, 0,
                $"Replies would go to {domain}, which is normal for mailing-list mail.");

        return new Finding(RuleId, RuleStatus.Flagged, Severity.Medium, 2,
            "Replies to this message would go to a different company than the one that appears to have sent it.");
    }

    private static string? FirstAddress(InternetAddressList? list)
    {
        if (list is null || list.Count == 0)
            return null;
        return list.Mailboxes.FirstOrDefault()?.Address;
    }
}