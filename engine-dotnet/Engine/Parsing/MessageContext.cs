using MimeKit;

namespace PhishAnalyser.Engine.Parsing;

/// <summary>
/// Delivery context that changes how much a header mismatch deserves to count.
/// Mailing lists and bulk senders legitimately use third-party bounce and reply domains.
/// </summary>
public static class MessageContext
{
    /// <summary>True when the message shows mailing-list or bulk-delivery headers.</summary>
    public static bool IsBulkOrList(MimeMessage message)
    {
        if (message.Headers.Contains("List-Id")) return true;
        if (message.Headers.Contains("List-Unsubscribe")) return true;
        if (message.Headers.Contains("List-Post")) return true;

        var precedence = message.Headers["Precedence"];
        return !string.IsNullOrEmpty(precedence)
               && (precedence.Equals("bulk", StringComparison.OrdinalIgnoreCase)
                   || precedence.Equals("list", StringComparison.OrdinalIgnoreCase));
    }
}