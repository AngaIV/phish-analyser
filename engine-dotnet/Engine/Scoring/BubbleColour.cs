namespace PhishAnalyser.Engine.Scoring;

/// <summary>What the user sees. Unknown exists so an unchecked message is never shown green.</summary>
public enum BubbleColour
{
    Green,
    Amber,
    Red,
    Unknown,
    Partial,
}