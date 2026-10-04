namespace Robotron2084.Hud;

/// <summary>
/// The arcade score display's digit model (notes §58.1), decoded from
/// DRAW_PLAYER_SCORES ($DC13) and the OS print routines ($6096, $610D).
///
/// The ROM stores a score as FOUR BCD bytes (<c>p1_score</c>) and walks them as
/// EIGHT digit positions, left to right:
///
///     ten-millions, millions, hundred-thousands, ten-thousands, thousands, hundreds, tens, units
///
/// The arcade masks the ten-millions digit off (<c>ANDA #$0F</c> on the top byte), so it only ever
/// shows seven. The port shows all eight (notes §139): a score is drawn with its ten-millions digit
/// when it has one.
///
/// A zero digit is SUPPRESSED while nothing has been printed yet — but the text
/// cursor still ADVANCES, by 6 px where a drawn glyph advances 7 px, so the
/// number stays left-anchored at the field's origin with narrower holes where
/// the leading zeros were. The ROM sets its "printed something" flag before the
/// LAST TWO positions ($DC4D: <c>INC $D6</c>), so tens and units are always
/// drawn: a fresh game shows "00" and 100 shows "100", never nothing.
/// </summary>
public static class ScoreFormatter
{
    /// <summary>Digit positions the ROM walks (10M, 1M, 100k, 10k, 1k, 100, 10, 1).</summary>
    public const int DigitPositions = 8;

    /// <summary>The largest score the eight drawn digits can show. The arcade stopped at 9,999,999 because it masks the ten-millions digit.</summary>
    public const int MaxScore = 99_999_999;

    /// <summary>
    /// The score's eight digit positions in the order the ROM draws them, with
    /// the leading zeros marked suppressed.
    /// </summary>
    public static ScoreDigit[] GetDigits(int score)
    {
        int value = System.Math.Clamp(score, 0, MaxScore);
        var digits = new ScoreDigit[DigitPositions];
        bool hasPrinted = false;

        for (int position = 0; position < DigitPositions; position++)
        {
            int digit = GetDigitAt(value, position);

            // ROM $610D: a zero digit takes the blank path while $D6 == 0
            // ("nothing printed yet") ... but $DC4D sets $D6 before the last two
            // positions, so tens and units always draw (score 0 prints "00").
            bool isInLastTwo = position >= DigitPositions - 2;
            bool isSuppressed = !hasPrinted && !isInLastTwo && digit == 0;

            if (!isSuppressed)
            {
                hasPrinted = true;
            }

            digits[position] = new ScoreDigit(digit, isSuppressed);
        }

        return digits;
    }

    /// <summary>
    /// The digits of the score with the ROM's leading-zero suppression applied,
    /// in reading order ("100", and "00" for a score of 0). Kept for callers that
    /// only need the digits, not the layout.
    /// </summary>
    public static int[] DrawnDigits(int score) =>
        GetDigits(score).Where(d => !d.Suppressed).Select(d => d.Value).ToArray();

    /// <summary>
    /// The glyphs to draw and their X positions, walking the ROM's cursor from
    /// <paramref name="originX"/>: a suppressed digit advances
    /// <paramref name="blankAdvancePixels"/>, a drawn one advances
    /// <paramref name="digitAdvancePixels"/>.
    /// </summary>
    public static IReadOnlyList<ScoreGlyph> LayOutGlyphs(int score, int originX, int digitAdvancePixels, int blankAdvancePixels)
    {
        var glyphs = new List<ScoreGlyph>(DigitPositions);
        int x = originX;

        foreach (ScoreDigit digit in GetDigits(score))
        {
            if (digit.Suppressed)
            {
                x += blankAdvancePixels;
            }
            else
            {
                glyphs.Add(new ScoreGlyph(digit.Value, x));
                x += digitAdvancePixels;
            }
        }

        return glyphs;
    }

    /// <summary>Position 0 = ten-millions (the ROM masks it off; the port draws it) ... 7 = units.</summary>
    private static int GetDigitAt(int score, int position)
    {
        int divisor = 1;
        for (int i = position; i < DigitPositions - 1; i++)
        {
            divisor *= 10;
        }

        return score / divisor % 10;
    }
}
