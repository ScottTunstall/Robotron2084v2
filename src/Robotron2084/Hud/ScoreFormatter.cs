using System.Collections.Generic;
using System.Linq;

namespace Robotron2084.Hud;

/// <summary>
/// The arcade score display's digit model (notes §58.1), decoded from
/// DRAW_PLAYER_SCORES ($DC13) and the OS print routines ($6096, $610D).
///
/// The ROM stores a score as FOUR BCD bytes (<c>p1_score</c>) and walks them as
/// EIGHT digit positions, left to right:
///
///     ten-millions (masked off with ANDA #$0F, so always blank),
///     millions, hundred-thousands, ten-thousands, thousands, hundreds, tens, units
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

    /// <summary>The largest score the seven drawn digits can show (the 10M digit is masked off).</summary>
    public const int MaxScore = 9_999_999;

    /// <summary>One digit position: its value, and whether the ROM blanks it.</summary>
    public readonly record struct ScoreDigit(int Value, bool Suppressed);

    /// <summary>A digit that is actually drawn, and the X the ROM's cursor puts it at.</summary>
    public readonly record struct ScoreGlyph(int Digit, int X);

    /// <summary>
    /// The score's eight digit positions in the order the ROM draws them, with
    /// the leading zeros (and the always-masked ten-millions digit) marked
    /// suppressed.
    /// </summary>
    public static ScoreDigit[] Digits(int score)
    {
        int value = System.Math.Clamp(score, 0, MaxScore);
        var digits = new ScoreDigit[DigitPositions];
        bool printed = false;

        for (int position = 0; position < DigitPositions; position++)
        {
            int digit = DigitAt(value, position);

            // ROM $610D: a zero digit takes the blank path while $D6 == 0
            // ("nothing printed yet") ... but $DC4D sets $D6 before the last two
            // positions, so tens and units always draw (score 0 prints "00").
            bool lastTwo = position >= DigitPositions - 2;
            bool suppressed = !printed && !lastTwo && digit == 0;

            if (!suppressed)
            {
                printed = true;
            }

            digits[position] = new ScoreDigit(digit, suppressed);
        }

        return digits;
    }

    /// <summary>
    /// The glyphs to draw and their X positions, walking the ROM's cursor from
    /// <paramref name="originX"/>: a suppressed digit advances
    /// <paramref name="blankAdvancePixels"/>, a drawn one advances
    /// <paramref name="digitAdvancePixels"/>.
    /// </summary>
    public static IReadOnlyList<ScoreGlyph> Layout(int score, int originX, int digitAdvancePixels, int blankAdvancePixels)
    {
        var glyphs = new List<ScoreGlyph>(DigitPositions);
        int x = originX;

        foreach (ScoreDigit digit in Digits(score))
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

    /// <summary>
    /// The digits of the score with the ROM's leading-zero suppression applied,
    /// in reading order ("100", and "00" for a score of 0). Kept for callers that
    /// only need the digits, not the layout.
    /// </summary>
    public static int[] DrawnDigits(int score) =>
        Digits(score).Where(d => !d.Suppressed).Select(d => d.Value).ToArray();

    /// <summary>Position 0 = ten-millions (always 0 — the ROM masks it off) ... 7 = units.</summary>
    private static int DigitAt(int score, int position)
    {
        int divisor = 1;
        for (int i = position; i < DigitPositions - 1; i++)
        {
            divisor *= 10;
        }

        return score / divisor % 10;
    }
}
