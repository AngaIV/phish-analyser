using MimeKit;
using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Rules;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Corpus;

public class AuthResultsCorpusTests
{
    private static readonly string CorpusRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "testdata"));

    [Fact]
    public void Corpus_ParsesAndClassifies_EverySample()
    {
        var rule = new AuthResultsRule();
        var unparseable = new List<string>();
        var missingOrNone = 0;
        var inconclusive = 0;

        foreach (var dir in new[] { "phishing", "legitimate" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(CorpusRoot, dir), "*.eml"))
            {
                using var stream = File.OpenRead(file);
                var message = MimeMessage.Load(stream);
                foreach (var f in rule.Evaluate(message))
                {
                    if (f.Status == RuleStatus.Inconclusive)
                    {
                        inconclusive++;
                    }
                    else if (f.Status == RuleStatus.Unknown)
                    {
                        if (f.Explanation.Contains("could not determine"))
                            unparseable.Add($"{Path.GetFileName(file)}: {f.Explanation}");
                        else
                            missingOrNone++;
                    }
                }
            }
        }

        Assert.True(unparseable.Count == 0,
            $"Unexpected auth result formats ({unparseable.Count}):{Environment.NewLine}{string.Join(Environment.NewLine, unparseable)}");
        Console.WriteLine($"Missing or unusable auth results (Unknown): {missingOrNone}");
        Console.WriteLine($"Mechanisms unreported alongside a usable result (Inconclusive): {inconclusive}");
    }
}