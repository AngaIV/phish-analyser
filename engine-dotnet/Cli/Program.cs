using PhishAnalyser.Engine.Parsing;
using PhishAnalyser.Engine.Rules;

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

var findings = new AuthResultsRule().Evaluate(message)
    .Concat(new IdentityMismatchRule().Evaluate(message))
    .ToList();

if (args.Contains("--json"))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        file = Path.GetFileName(args[0]),
        warnings = result.Warnings,
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

foreach (var finding in findings)
    Console.WriteLine($"[{finding.Status}] {finding.Severity} {finding.RuleId}: {finding.Explanation}");

return 0;