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

foreach (var finding in new AuthResultsRule().Evaluate(message))
    Console.WriteLine($"[{finding.Status}] {finding.Severity} {finding.RuleId}: {finding.Explanation}");

return 0;