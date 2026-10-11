using PhishAnalyser.Engine.Models;
using PhishAnalyser.Engine.Scoring;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Scoring;

public class ScorerTests
{
    private static Finding F(RuleStatus status, Severity severity, double weight) =>
        new("test", status, severity, weight, "explanation");

    [Fact]
    public void AllPass_IsGreenWithZero()
    {
        var verdict = Scorer.Score([F(RuleStatus.Pass, Severity.Info, 0)]);

        Assert.Equal(0, verdict.Number);
        Assert.Equal(BubbleColour.Green, verdict.Colour);
    }

    [Fact]
    public void SingleLowWeightFinding_IsGreenWithOne()
    {
        var verdict = Scorer.Score([F(RuleStatus.Flagged, Severity.Low, 1)]);

        Assert.Equal(1, verdict.Number);
        Assert.Equal(BubbleColour.Green, verdict.Colour);
    }

    [Fact]
    public void WeightTwo_IsAmber()
    {
        var verdict = Scorer.Score([F(RuleStatus.Flagged, Severity.Medium, 2)]);

        Assert.Equal(BubbleColour.Amber, verdict.Colour);
    }

    [Fact]
    public void WeightThree_IsAmber()
    {
        var verdict = Scorer.Score([F(RuleStatus.Flagged, Severity.High, 3)]);

        Assert.Equal(BubbleColour.Amber, verdict.Colour);
    }

    [Fact]
    public void WeightFour_IsRed()
    {
        var verdict = Scorer.Score([F(RuleStatus.Flagged, Severity.High, 4)]);

        Assert.Equal(BubbleColour.Red, verdict.Colour);
    }

    [Fact]
    public void TwoSmallFindings_AddUpToAmber()
    {
        var verdict = Scorer.Score([
            F(RuleStatus.Flagged, Severity.Low, 1),
            F(RuleStatus.Flagged, Severity.Low, 1),
        ]);

        Assert.Equal(2, verdict.Number);
        Assert.Equal(BubbleColour.Amber, verdict.Colour);
    }

    [Fact]
    public void AllUnknown_IsNotGreen()
    {
        // The fail-safe: an unchecked message must never look safe.
        var verdict = Scorer.Score([
            F(RuleStatus.Unknown, Severity.Info, 0),
            F(RuleStatus.Unknown, Severity.Info, 0),
        ]);

        Assert.Equal(0, verdict.Number);
        Assert.Equal(BubbleColour.Unknown, verdict.Colour);
    }

    [Fact]
    public void NoFindingsAtAll_IsGreen()
    {
        var verdict = Scorer.Score([]);

        Assert.Equal(BubbleColour.Green, verdict.Colour);
    }

    [Fact]
    public void NumberCountsFindings_NotWeight()
    {
        var verdict = Scorer.Score([
            F(RuleStatus.Flagged, Severity.High, 4),
            F(RuleStatus.Flagged, Severity.Low, 1),
        ]);

        Assert.Equal(2, verdict.Number);
        Assert.Equal(5, verdict.Score);
        Assert.Equal(BubbleColour.Red, verdict.Colour);
    }
}