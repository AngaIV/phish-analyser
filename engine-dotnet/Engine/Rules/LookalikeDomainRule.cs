using System.Text.RegularExpressions;
using MimeKit;
using PhishAnalyser.Engine.Configuration;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Parsing;

namespace PhishAnalyser.Engine.Rules;

public sealed class LookalikeDomainRule : IRule
{
    public string RuleId => "lookalike-domain";

    /// <summary>
    /// Free or heavily-abused TLDs. Abused, not automatically hostile on its own — but
    /// weight 2, because a message carrying only this signal must not be shown as safe.
    /// </summary>
    private static readonly HashSet<string> CheapTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ga", "ml", "cf", "gq", "tk", "xyz", "top", "icu", "buzz", "cyou", "click", "monster",
    };

    /// <summary>A host label built from an IP, such as "v133-18-229-1".</summary>
    private static readonly Regex IpDerivedLabel =
        new(@"^v?\d{1,3}([-.]\d{1,3}){2,}$", RegexOptions.Compiled);

    /// <summary>A run of four or more digits, which "mx01" does not match but "em5269" does.</summary>
    private static readonly Regex LongDigitRun = new(@"\d{4,}", RegexOptions.Compiled);

    /// <summary>Brands shorter than this are excluded — "ups" matches arbitrary words as a substring.</summary>
    private const int MinBrandLength = 4;

    public IReadOnlyList<Finding> Evaluate(MimeMessage message)
    {
        var mailbox = message.From.Mailboxes.FirstOrDefault();
        if (mailbox?.Address is null)
            return Single(RuleStatus.Unknown, Severity.Info, 0,
                "The sender could not be read, so the sender's web address could not be checked.");

        var host = DomainHelper.Host(mailbox.Address);
        var registrable = DomainHelper.RegistrableDomain(mailbox.Address);
        if (host is null || registrable is null)
            return Single(RuleStatus.Unknown, Severity.Info, 0,
                "The sender's web address could not be read, so it could not be checked.");

        var domainLabel = registrable.Split('.')[0];

        var brand = FindBrandWithLureWord(host, domainLabel);
        if (brand is not null)
            return Single(RuleStatus.Flagged, Severity.High, 3,
                $"The sender's web address combines the name {brand} with a word designed to pressure you. It is not {brand}'s real address ({host}).");

        var signals = new List<string>();

        var tld = host.Split('.')[^1];
        if (CheapTlds.Contains(tld))
            signals.Add($"it uses the ending .{tld}, often used for disposable and fraudulent sites");

        var leftLabel = host.Split('.')[0];
        var digitHeavy = IpDerivedLabel.IsMatch(leftLabel) || LongDigitRun.IsMatch(leftLabel);
        if (digitHeavy)
            signals.Add("it looks like it was generated automatically rather than registered by a company");

        if (signals.Count > 0)
            return Single(RuleStatus.Flagged, Severity.Medium, 2,
                $"The sender's web address ({host}) looks unusual: {string.Join(", and ", signals)}.");

        return Single(RuleStatus.Pass, Severity.Info, 0,
            $"The sender's web address ({host}) looks normal.");
    }

    /// <summary>
    /// Finds a brand whose own domain this is not, appearing together with a lure word.
    /// Returns null when nothing matches.
    /// </summary>
    private static string? FindBrandWithLureWord(string host, string domainLabel)
    {
        var flattened = host.Replace("-", string.Empty).Replace(".", string.Empty);

        foreach (var brand in BrandList.Brands.Keys.OrderBy(b => b, StringComparer.OrdinalIgnoreCase))
        {
            if (brand.Length < MinBrandLength)
                continue;
            if (!flattened.Contains(brand, StringComparison.OrdinalIgnoreCase))
                continue;

            // The brand's own domains and aliases are legitimate; support.apple.com is not a lookalike.
            if (BrandList.IsAlignedDomain(domainLabel, brand))
                continue;

            if (LureWords.All.Any(w => flattened.Contains(w, StringComparison.OrdinalIgnoreCase)))
                return brand;
        }

        return null;
    }

    private IReadOnlyList<Finding> Single(RuleStatus status, Severity severity, double weight, string explanation) =>
        new[] { new Finding(RuleId, status, severity, weight, explanation) };
}