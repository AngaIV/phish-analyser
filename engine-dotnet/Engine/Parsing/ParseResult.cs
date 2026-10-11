using MimeKit;

namespace PhishAnalyser.Engine.Parsing;

/// <summary>Outcome of a parse attempt. Failure is a value, never an exception.</summary>
public sealed record ParseResult(MimeMessage? Message, IReadOnlyList<string> Warnings)
{
    public bool Succeeded => Message is not null;
}