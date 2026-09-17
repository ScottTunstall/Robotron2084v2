using System.Linq;
using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// The arcade score display's digit model (notes §58.1, from $DC13's
/// DRAW_PLAYER_SCORES): eight digit positions (ten-millions, always masked off,
/// then 1M..units), leading zeros suppressed but still advancing the cursor, and
/// the LAST TWO digits always drawn.
/// </summary>
public class ScoreFormatterTests
{
    private static string Drawn(int score) => string.Concat(ScoreFormatter.DrawnDigits(score).Select(d => d.ToString()));

    [Fact]
    public void ZeroScore_ShowsTwoDigits()
    {
        // ROM $DC4D sets its "printed something" flag before the last two digit
        // positions, so a fresh game shows "00" — it is never blank.
        Assert.Equal("00", Drawn(0));
    }

    [Fact]
    public void LeadingZeros_Suppressed()
    {
        Assert.Equal("100", Drawn(100));

        // The last two digit positions are ALWAYS drawn, so a score under 100
        // keeps its tens zero: 5 prints "05", not "5".
        Assert.Equal("05", Drawn(5));
    }

    [Fact]
    public void InteriorZeros_Kept()
    {
        Assert.Equal("1002", Drawn(1002));
    }

    [Fact]
    public void FullScore_AllSevenDigitsDrawn()
    {
        Assert.Equal("9999999", Drawn(9_999_999));
    }

    [Fact]
    public void NegativeScore_IsTreatedAsZero()
    {
        Assert.Equal("00", Drawn(-500));
    }

    [Fact]
    public void Digits_AlwaysReportsEightPositions_AndSuppressesTheLeadingOnes()
    {
        ScoreFormatter.ScoreDigit[] digits = ScoreFormatter.Digits(100);

        Assert.Equal(ScoreFormatter.DigitPositions, digits.Length);

        // 10M, 1M, 100k, 10k and 1k are suppressed; 100, 10 and 1 are drawn.
        Assert.True(digits.Take(5).All(d => d.Suppressed));
        Assert.False(digits[5].Suppressed);
        Assert.Equal(1, digits[5].Value);
        Assert.False(digits[7].Suppressed);
        Assert.Equal(0, digits[7].Value);
    }

    [Fact]
    public void Layout_AdvancesSevenPixelsPerDigit_AndSixPerSuppressedZero()
    {
        // Score 100 at origin 100: five suppressed zeros (6 px each = 30) put the
        // "1" at 130, then 10s at 137 and units at 144 (7 px steps).
        var glyphs = ScoreFormatter.Layout(100, originX: 100, digitAdvancePixels: 7, blankAdvancePixels: 6);

        Assert.Equal([(1, 130), (0, 137), (0, 144)], glyphs.Select(g => (g.Digit, g.X)));
    }

    [Fact]
    public void Layout_ZeroScore_DrawsBothDigitsAfterSixBlanks()
    {
        // Six suppressed zeros (the 10M digit plus 1M..100) = 36 px, then "00".
        var glyphs = ScoreFormatter.Layout(0, originX: 0, digitAdvancePixels: 7, blankAdvancePixels: 6);

        Assert.Equal([(0, 36), (0, 43)], glyphs.Select(g => (g.Digit, g.X)));
    }
}
