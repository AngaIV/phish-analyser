using PhishAnalyser.Desktop.Models;
using PhishAnalyser.Engine.Parsing;
using PhishAnalyser.Engine.Rules;
using PhishAnalyser.Engine.Scoring;

namespace PhishAnalyser.Desktop.Analysis;

/// <summary>
/// Turns a raw header block into a bubble view. The only place the UI meets the engine.
/// </summary>
public static class HeaderAnalyser
{
    public static BubbleView Analyse(string? headerBlock)
    {
        // No headers at all: not analysed. Never "no issues found".
        if (string.IsNullOrWhiteSpace(headerBlock))
            return Unreadable();

        var parse = SafeMessageLoader.LoadFromHeaderBlock(headerBlock);

        if (!parse.Succeeded)
            return Unreadable();

        try
        {
            var findings = RuleSet.EvaluateAll(parse.Message!);
            var verdict = Scorer.Score(findings);
            return BubbleViewFactory.FromVerdict(verdict);
        }
        catch (Exception)
        {
            // A rule must not be able to take the app down. Fail to the grey state.
            return Unreadable();
        }
    }

    private static BubbleView Unreadable() => new(
        BubbleState.Unknown, 0,
        "Could not read this message",
        "The message headers were unavailable, so this message has not been checked.");
}