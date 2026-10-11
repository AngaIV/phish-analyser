namespace PhishAnalyser.Engine.Configuration;

/// <summary>
/// Social-engineering words that appear alongside a brand name in fake domains
/// ("paypal-secure.com"). Data, not logic — moves to configuration with D-005.
/// Only words of 5+ characters are used; short generic words ("id", "mail") match far
/// too much to be evidence.
/// </summary>
public static class LureWords
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "secure", "security", "login", "signin", "verify", "verification",
        "account", "accounts", "update", "billing", "invoice", "payment",
        "alert", "confirm", "confirmation", "recovery", "unlock",
        "service", "support", "helpdesk", "tracking", "wallet",
    };
}