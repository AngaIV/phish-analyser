using PhishAnalyser.Engine.Parsing;
using PhishAnalyser.Engine.Rules;
using PhishAnalyser.Engine.Scoring;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Cli <path-to-eml-file> [--headers]");
    return 1;
}

var showHeaders = args.Contains("--headers");
var result = SafeMessageLoader.Load(args[0]);

foreach (var warning in result.Warnings)
    Console.WriteLine($"warning: {warning}");

if (!result.Succeeded)
{
    Console.Error.WriteLine("could not parse");
    return 1;
}

var message = result.Message!;

if (showHeaders)
    foreach (var header in message.Headers)
        Console.WriteLine($"header: {header.Field} = {header.Value.Replace("\n", " ")}");

var findings = RuleSet.EvaluateAll(message);
var verdict = Scorer.Score(findings);

if (args.Contains("--json"))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        file = Path.GetFileName(args[0]),
        warnings = result.Warnings,
        number = verdict.Number,
        colour = verdict.Colour.ToString().ToLowerInvariant(),
        score = verdict.Score,
        findings = findings.Select(f => new
        {
            rule = f.RuleId,
            status = f.Status.ToString().ToLowerInvariant(),
            severity = f.Severity.ToString().ToLowerInvariant(),
            weight = f.Weight,
        }),
    }));
    return 0;
}

Console.WriteLine($"bubble: {verdict.Number} {verdict.Colour.ToString().ToLowerInvariant()} (score {verdict.Score})");

foreach (var finding in findings)
    Console.WriteLine($"[{finding.Status}] {finding.Severity} {finding.RuleId}: {finding.Explanation}");

return 0;