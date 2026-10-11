using PhishAnalyser.Engine.Models;

namespace PhishAnalyser.Engine.Scoring;

///<summary>Turns findings into the bubble number and colour.</summary>
public static class Scorer
{
    ///<summary>Score at or above which the bubble turns amber.</summary>
    public const double AmberThreshold = 2;

    ///<summary>Score at or above which the bubble turns red.</summary>
    public const double RedThreshold = 4;

    public static Verdict Score(IReadOnlyList<Finding> findings)
    {
        var flagged = findings.Where(f => f.Status == RuleStatus.Flagged).ToList();
        var score = flagged.Sum(f => f.Weight);

        return new Verdict(flagged.Count, Colour(flagged.Count, score, findings), score, findings);
    }

    private static BubbleColour Colour(int flaggedCount, double score, IReadOnlyList<Finding> findings)
    {
        if (score >= RedThreshold) return BubbleColour.Red;
        if (score >= AmberThreshold) return BubbleColour.Amber;

        // Nothing alarming: show grey if anything went unchecked, otherwise green.
        return findings.Any(f => f.Status == RuleStatus.Unknown)
            ? BubbleColour.Unknown
            : BubbleColour.Green;
    }
}