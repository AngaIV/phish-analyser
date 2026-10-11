using System.Text.RegularExpressions;
using MimeKit;
using PhishAnalyser.Engine.Configuration;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Parsing;

namespace PhishAnalyser.Engine.Rules;

public sealed class DisplayNameSpoofRule : IRule
{
    public string RuleId => "display-name-spoof";

    ///<summary>An email address appearing inside a display name.</summary>
    private static readonly Regex AddressInName =
        new(@"[^\s<>@]+@[^\s<>@]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    ///<summary>Word-boundary brand matchers, built once. Boundaries stop "ups" matching "Startups".</summary>
    private static readonly Dictionary<string, Regex> BrandPatterns =
        BrandList.Brands.Keys.ToDictionary(
            brand => brand,
            brand => new Regex(
                $@"\b{Regex.Escape(brand)}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Finding> Evaluate(MimeMessage message)
    {
        var mailbox = message.From.Mailboxes.FirstOrDefault();
        if (mailbox?.Address is null)
            return Single(RuleStatus.Unknown, Severity.Info, 0,
                "The sender could not be read, so the sender's name could not be checked.");

        var fromDomain = DomainHelper.RegistrableDomain(mailbox.Address);
        if (fromDomain is null)
            return Single(RuleStatus.Unknown, Severity.Info, 0,
                "The sender's address could not be read, so the sender's name could not be checked.");

        var displayName = mailbox.Name?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return Single(RuleStatus.Unknown, Severity.Info, 0,
                "This message has no sender name, so there is nothing to compare.");

        var domainLabel = fromDomain.Split('.')[0];

        //Ordered for deterministic output when a name mentions more than one brand.
        foreach (var brand in BrandList.Brands.Keys.OrderBy(b => b, StringComparer.Ordinal))
        {
            if (!BrandPatterns[brand].IsMatch(displayName))
                continue;

            if (BrandList.IsAlignedDomain(domainLabel, brand))
                return Single(RuleStatus.Pass, Severity.Info, 0,
                    $"The sender's name says {displayName}, which matches the domain it was sent from ({fromDomain}).");

            return Single(RuleStatus.Flagged, Severity.High, 4,
                $"This message claims to be from {displayName} in the sender's name, but it was not sent from that company's email domain ({fromDomain}).");
        }

        var addressMatch = AddressInName.Match(displayName);
        if (addressMatch.Success)
        {
            var namedDomain = DomainHelper.RegistrableDomain(addressMatch.Value);

            if (namedDomain is not null
                && !namedDomain.Equals(fromDomain, StringComparison.OrdinalIgnoreCase))
            {
                return Single(RuleStatus.Flagged, Severity.Medium, 2,
                    $"The sender's name is an email address ({addressMatch.Value}) that does not match the address the message actually came from ({fromDomain}).");
            }
        }

        return Single(RuleStatus.Pass, Severity.Info, 0,
            "The sender's name does not claim to be anyone other than the address it was sent from.");
    }

        private IReadOnlyList<Finding> Single(RuleStatus status, Severity severity, double weight, string explanation) =>
            new[] { new Finding(RuleId, status, severity, weight, explanation) };
}