using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Rules;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Cli <path-to-eml-file>");
    return 1;
}

try
{
    using var stream = File.OpenRead(args[0]);
    var message = MimeMessage.Load(stream);

    var rule = new AuthResultsRule();
    foreach (var finding in rule.Evaluate(message))
    {
        Console.WriteLine($"[{finding.Status}] {finding.Severity} {finding.RuleId}: {finding.Explanation}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"could not parse: {ex.Message}");
    return 1;
}

return 0;