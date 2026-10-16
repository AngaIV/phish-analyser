using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Rules;
using System.Text.RegularExpressions;

namespace PhishAnalyser.Engine.Rules;

public sealed class AuthResultsRule : IRule
{
    public string RuleId => "auth-results";

    private static readonly (string Mechanism, string Label)[] Mechanisms =
    {
        ("spf", "SPF"),
        ("dkim", "DKIM"),
        ("dmarc", "DMARC"),
    };

    public IReadOnlyList<Finding> Evaluate(MimeMessage message)
    {
        var findings = new List<Finding>();
        var headers = message.Headers.Where(h =>
            h.Field.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase));

        var results = Mechanisms
            .Select(mechanism => (
                mechanism.Label,
                Result: ExtractResult(headers, mechanism.Mechanism)))
            .ToList();

        // A missing mechanism is only inconclusive when another mechanism actually
        // reported a usable result. An auth=none header by itself remains Unknown.
        var hasUsableResult = results.Any(result => IsUsableResult(result.Result));

        foreach (var result in results)
            findings.Add(Classify(result.Result, result.Label, hasUsableResult));

        return findings;
    }

    private static string? ExtractResult(IEnumerable<MimeKit.Header> headers, string mechanism)
    {
        // Match: "<mechanism>=<word>" where <word> is stop, pass, fail, softfail,
        // neutral, none, temperror or permerror. Case-insensitive.
        var pattern = $@"(?i)\b{mechanism}=(pass|fail|softfail|neutral|none|temperror|permerror)\b";
        var match = headers
            .Select(h => h.Value)
            .Select(v => Regex.Match(v, pattern))
            .FirstOrDefault(m => m.Success);

        return match?.Success == true ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static bool IsUsableResult(string? result) => result is
        "pass" or "fail" or "softfail" or "permerror";

    private static Finding Classify(string? result, string label, bool hasUsableResult)
    {
        switch (result)
        {
            case null:
            case "none":
            case "neutral":
                var status = hasUsableResult ? RuleStatus.Inconclusive : RuleStatus.Unknown;
                return new Finding("auth-results", status, Severity.Info, 0,
                    hasUsableResult
                        ? $"{label}: no conclusive result was reported."
                        : $"{label}: no result recorded (missing header, forwarded mail, or 'none').");
            case "pass":
                return new Finding("auth-results", RuleStatus.Pass, Severity.Info, 0,
                    $"{label}: pass.");
            case "fail":
            case "permerror":
                return new Finding("auth-results", RuleStatus.Flagged, Severity.Medium, 3,
                    $"{label}: fail — the sending server is not authorised for this domain.");
            case "softfail":
                return new Finding("auth-results", RuleStatus.Flagged, Severity.Low, 1,
                    $"{label}: softfail — the sending server is probably not authorised.");
            case "temperror":
                return new Finding("auth-results", RuleStatus.Unknown, Severity.Info, 0,
                    $"{label}: temporary verification error.");
            default:
                return new Finding("auth-results", RuleStatus.Unknown, Severity.Info, 0,
                    $"{label}: could not determine result.");
        }
    }
}