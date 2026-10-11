using MimeKit;

namespace PhishAnalyser.Engine.Parsing;

/// <summary>
/// Extracts the registrable domain from header address values.
/// Deliberately dependency-free: a minimal second-level-label set covers the
/// common co.za / co.uk / com.au cases. A full public-suffix list is a later upgrade.
/// </summary>
public static class DomainHelper
{
    private static readonly HashSet<string> SecondLevelLabels =
        new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "net", "gov", "ac", "edu" };

    ///<summary>Registrable domain of an address such as "Name &lt;a@mail.example.co.za&gt;".</summary>
    public static string? RegistrableDomain(string? rawAddress)
    {
        var host = Host(rawAddress);
        if (host is null)
            return null;

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2)
            return null;

        var twoLetterTld = labels[^1].Length == 2;
        if (labels.Length >= 3 && twoLetterTld && SecondLevelLabels.Contains(labels[^2]))
            return string.Join('.', labels[^3..]);

        return string.Join('.', labels[^2..]);
    }

    ///<summary>Host part of an address, without the local part. Null when there is no usable domain.</summary>
    public static string? Host(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return null;

        var text = rawAddress.Trim();

        if (MailboxAddress.TryParse(text, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox.Address))
            text = mailbox.Address;

        var at = text.LastIndexOf('@');
        if (at < 0 || at == text.Length - 1)
            return null;

        var host = text[(at + 1)..].Trim().TrimEnd('>', '.', ';').ToLowerInvariant();
        return host.Contains('.') ? host : null;
    }
}