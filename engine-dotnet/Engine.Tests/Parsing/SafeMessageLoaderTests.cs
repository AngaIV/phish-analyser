using PhishAnalyser.Engine.Parsing;
using Xunit;

namespace PhishAnalyser.Engine.Tests.Parsing;

public class SafeMessageLoaderTests
{
    private static readonly string Testdata = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "testdata"));

    private static string Malformed(string name) => Path.Combine(Testdata, "malformed", name);

    [Theory]
    [InlineData("truncated.eml")]
    [InlineData("duplicate_from.eml")]
    [InlineData("no_headers.eml")]
    [InlineData("missing_colon.eml")]
    [InlineData("binary_junk.eml")]
    [InlineData("null_bytes.eml")]
    [InlineData("deeply_folded.eml")]
    [InlineData("broken_truncated.eml")]
    public void NeverThrowsOnMalformedInput(string name)
    {
        var result = SafeMessageLoader.Load(Malformed(name));
        Assert.NotNull(result.Warnings);   //an outcome, not an exception
    }

    [Fact]
    public void DuplicateFromIsReportedAsWarning()
    {
        var result = SafeMessageLoader.Load(Malformed("duplicate_from.eml"));
        Assert.Contains(result.Warnings, w => w.Contains("multiple From"));
    }

    [Fact]
    public void MissingFileIsReportedNotThrown()
    {
        var result = SafeMessageLoader.Load(Malformed("does_not_exist.eml"));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Warnings, w => w.Contains("file not found"));
    }

    [Fact]
    public void TenMegabyteHeaderBlockIsHandledWithinTheCeiling()
    {
        var temp = Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(temp))
            {
                fs.Write("X-Padding: "u8);
                var chunk = new byte[64 * 1024];
                Array.Fill(chunk, (byte)'A');
                for (var i = 0; i < 160; i++) fs.Write(chunk);   // 10 MB
                fs.Write("\nFrom: a@b.c\n\n"u8);
            }

            var result = SafeMessageLoader.Load(temp);

            Assert.True(result.Succeeded, string.Join("; ", result.Warnings));
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void OversizedFileIsRejectedWithoutParsing()
    {
        var temp = Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(temp)) fs.SetLength(SafeMessageLoader.MaxBytes + 1);

            var result = SafeMessageLoader.Load(temp);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Warnings, w => w.Contains("exceeds"));
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void ValidSampleStillParses()
    {
        var good = Path.Combine(Testdata, "legitimate", "ham_003_rhn_errata_alert.eml");
        var result = SafeMessageLoader.Load(good);
        Assert.True(result.Succeeded);
    }
}