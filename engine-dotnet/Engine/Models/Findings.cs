namespace PhishAnalyser.Engine.Models;

public sealed record Finding(
    string RuleId,
    RuleStatus Status,
    Severity Severity,
    double Weight,
    string Explanation);