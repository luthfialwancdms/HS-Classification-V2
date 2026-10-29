using HsClassifier.Infrastructure.Seeding;

namespace HsClassifier.Tests;

/// <summary>MFN text from the OCR'd BTKI table: accept only clean percentages, never guess.</summary>
public class MfnParseTests
{
    [Theory]
    [InlineData("5,00%", 5.00)]
    [InlineData("10.00%", 10.00)]
    [InlineData("0,00%", 0.00)]
    [InlineData(" 15,00 % ", 15.00)]
    [InlineData("7,5%", 7.5)]
    public void Clean_percentages_parse(string raw, double expected) =>
        Assert.Equal((decimal)expected, BtkiCsvSeeder.ParseMfnPercent(raw));

    [Theory]
    [InlineData("")]
    [InlineData("ii,00%")]   // OCR garbage
    [InlineData("'-,00%")]
    [InlineData("5,00")]     // no percent sign → unit unknown
    [InlineData("150,00%")]  // outside 0–100
    [InlineData("Rp 1.000/kg")] // specific rate — not a percentage
    public void Garbage_is_rejected(string raw) => Assert.Null(BtkiCsvSeeder.ParseMfnPercent(raw));
}
