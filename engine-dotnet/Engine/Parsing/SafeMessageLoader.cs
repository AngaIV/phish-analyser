using MimeKit;

namespace PhishAnalyser.Engine.Parsing;

/// <summary>
/// Parse wrapper for untrusted input. Guarantees: no exception escapes, a size
/// ceiling is enforced before parsing, and every anomaly is surfaced as a warning.
/// </summary>
public static class SafeMessageLoader
{
    public const long MaxBytes = 20 * 1024 * 1024;
    public const int MaxHeaderCount = 2000;

    public static ParseResult Load(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return Failure($"file not found: {Path.GetFileName(path)}");
            if (info.Length > MaxBytes)
                return Failure($"file exceeds {MaxBytes} byte limit ({info.Length} bytes)");

            using var stream = File.OpenRead(path);
            var message = MimeMessage.Load(stream);
            return new ParseResult(message, Inspect(message));
        }
        catch (Exception ex)
        {
            //Deliberately broad: hostile input must never escape as an exception.
            return Failure($"could not parse: {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Parses a raw header block that is already in memory, such as the transport headers
    /// read from a mail client. Same guarantees as <see cref="Load"/>: no exception escapes,
    /// the size ceiling still applies, and anomalies are warnings.
    /// <para>
    /// Exists so headers never have to be written to disk to be analysed. Persisting message
    /// metadata for every message a user opens would be a privacy problem, and the engine
    /// only ever needs the block in memory.
    /// </para>
    /// </summary>
    public static ParseResult LoadFromHeaderBlock(string headerBlock)
    {
        if (string.IsNullOrWhiteSpace(headerBlock))
            return Failure("no headers supplied");

        try
        {
            // Normalise the terminator. A header block that ends without a blank line is
            // valid for MimeKit but makes the header/body boundary implicit; making it
            // explicit keeps parsing deterministic across callers.
            var block = headerBlock.EndsWith("\r\n\r\n", StringComparison.Ordinal)
                     || headerBlock.EndsWith("\n\n", StringComparison.Ordinal)
                ? headerBlock
                : headerBlock.TrimEnd('\r', '\n') + "\r\n\r\n";

            var bytes = System.Text.Encoding.UTF8.GetBytes(block);
            if (bytes.LongLength > MaxBytes)
                return Failure($"headers exceed {MaxBytes} byte limit ({bytes.LongLength} bytes)");

            using var stream = new MemoryStream(bytes, writable: false);
            var message = MimeMessage.Load(stream);
            return new ParseResult(message, Inspect(message));
        }
        catch (Exception ex)
        {
            //Deliberately broad: hostile input must never escape as an exception.
            return Failure($"could not parse: {ex.GetType().Name}");
        }
    }

    private static ParseResult Failure(string reason) => new(null, new[] { reason });

    private static List<string> Inspect(MimeMessage message)
    {
        var warnings = new List<string>();
        var headers = message.Headers;

        if (headers.Count == 0)
        {
            warnings.Add("no headers found");
            return warnings;
        }

        if (headers.Count > MaxHeaderCount)
            warnings.Add($"unusually many headers ({headers.Count})");
        if (Count(headers, "From") > 1)
            warnings.Add("multiple From headers");
        if (Count(headers, "Return-Path") > 1)
            warnings.Add("multiple Return-Path headers");
        if (Count(headers, "From") == 0)
            warnings.Add("missing From header");
        if (Count(headers, "Received") == 0)
            warnings.Add("missing Received header");

        return warnings;
    }

    private static int Count(MimeKit.HeaderList headers, string field) =>
        headers.Count(h => h.Field.Equals(field, StringComparison.OrdinalIgnoreCase));
}