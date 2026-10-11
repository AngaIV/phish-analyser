namespace PhishAnalyser.Engine.Configuration;

/// <summary>
/// Brands commonly impersonated in phishing, each mapped to the registrable-domain
/// second-level labels that legitimately belong to it. This is data, not logic: it moves
/// to a configuration file when the scoring module lands (decision D-005), so the list can
/// be extended without recompiling.
/// </summary>
public static class BrandList
{
    public static IReadOnlyDictionary<string, string[]> Brands { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Payments and finance
            ["paypal"] = ["paypal"],
            ["coinbase"] = ["coinbase"],
            ["binance"] = ["binance"],
            ["chase"] = ["chase"],
            ["wellsfargo"] = ["wellsfargo"],
            ["citibank"] = ["citibank"],
            ["hsbc"] = ["hsbc"],
            ["barclays"] = ["barclays"],

            // South African banks and institutions
            ["nedbank"] = ["nedbank"],
            ["standardbank"] = ["standardbank"],
            ["capitec"] = ["capitec"],
            ["absa"] = ["absa"],
            ["fnb"] = ["fnb"],
            ["sars"] = ["sars"],
            ["telkom"] = ["telkom"],

            // Technology
            ["microsoft"] = ["microsoft", "office", "outlook", "live", "msn"],
            ["google"] = ["google", "gmail", "youtube"],
            ["apple"] = ["apple", "icloud"],
            ["amazon"] = ["amazon", "aws"],
            ["adobe"] = ["adobe"],
            ["dropbox"] = ["dropbox"],
            ["linkedin"] = ["linkedin"],
            ["zoom"] = ["zoom"],
            ["netflix"] = ["netflix"],
            ["spotify"] = ["spotify"],
            ["steam"] = ["steampowered", "steamcommunity"],

            // Document sharing and logistics
            ["docusign"] = ["docusign"],
            ["wetransfer"] = ["wetransfer"],
            ["onedrive"] = ["onedrive", "microsoft", "live"],
            ["sharepoint"] = ["sharepoint", "microsoft"],
            ["dhl"] = ["dhl"],
            ["fedex"] = ["fedex"],
            ["ups"] = ["ups"],
            ["usps"] = ["usps"],
            ["icann"] = ["icann"],
        };

    ///<summary>True when a domain label legitimately belongs to the named brand.</summary>
    public static bool IsAlignedDomain(string domainLabel, string brand) =>
        Brands.TryGetValue(brand, out var labels)
        && labels.Any(l => l.Equals(domainLabel, StringComparison.OrdinalIgnoreCase));
}