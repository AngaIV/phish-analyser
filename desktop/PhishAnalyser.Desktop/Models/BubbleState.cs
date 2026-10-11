namespace PhishAnalyser.Desktop.Models;

/// <summary>What the bubble is currently showing.</summary>
public enum BubbleState
{
    /// <summary>No verdict yet — the message has just been selected.</summary>
    Analysing,

    /// <summary>Analysis completed and nothing was flagged. Never means "safe".</summary>
    NoIssues,

    /// <summary>Something was flagged, not enough to be alarming.</summary>
    Warning,

    /// <summary>Something serious was flagged.</summary>
    Danger,

    /// <summary>Analysis could not complete. Must never be shown as NoIssues.</summary>
    Unknown,

    /// <summary>The user snoozed or disabled the bubble for this message.</summary>
    Hidden,
}

/// <summary>
/// Everything the overlay needs to render one message. Deliberately a plain record with
/// no engine types, so the UI cannot depend on rule internals.
/// </summary>
public sealed record BubbleView(
    BubbleState State,
    int Number,
    string Headline,
    string Detail);