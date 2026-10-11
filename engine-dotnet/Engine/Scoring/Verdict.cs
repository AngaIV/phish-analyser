using PhishAnalyser.Engine.Models;

namespace PhishAnalyser.Engine.Scoring;

///<summary>The scored result for one message: the bubble number and colour.</summary>
public sealed record Verdict(int Number, BubbleColour Colour, double Score, IReadOnlyList<Finding> Findings);