using PhishAnalyser.Engine.Scoring;

namespace PhishAnalyser.Desktop.Models;

/// <summary>Turns an engine verdict into what the bubble shows.</summary>
public static class BubbleViewFactory
{
    public static BubbleView FromVerdict(Verdict verdict) => verdict.Colour switch
    {
        // D-011: never "safe", never a tick. The engine found nothing; that is all this says.
        BubbleColour.Green => new BubbleView(
            BubbleState.NoIssues, verdict.Number,
            "No issues found",
            "Nothing in this message's headers looked suspicious."),

        BubbleColour.Amber => new BubbleView(
            BubbleState.Warning, verdict.Number,
            "Worth a closer look",
            "Some details of this message looked unusual."),

        BubbleColour.Red => new BubbleView(
            BubbleState.Danger, verdict.Number,
            "Likely phishing",
            "This message shows several signs of a phishing attempt."),

        // The fail-safe. Reached when a rule could not check something.
        BubbleColour.Unknown => new BubbleView(
            BubbleState.Unknown, verdict.Number,
            "Could not check this message",
            "The message could not be analysed, so no result is available."),

        // A real result exists, but the mail server did not report every mechanism.
        // This is neither green nor a parser failure.
        BubbleColour.Partial => new BubbleView(
            BubbleState.Partial, verdict.Number,
            "Some checks not reported",
            "At least one check returned a result. The sender's mail server did not report all checks."),

        // If a colour is ever added to the enum, fail safe rather than showing all-clear.
        _ => new BubbleView(
            BubbleState.Unknown, verdict.Number,
            "Could not analyse this message",
            "The message's headers could not be read."),
    };

    public static BubbleView Analysing() => new(
        BubbleState.Analysing, 0, "Checking this message...", "Reading the message headers.");
}