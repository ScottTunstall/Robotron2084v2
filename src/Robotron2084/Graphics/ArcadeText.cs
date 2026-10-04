using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Tuning;

namespace Robotron2084.Graphics;

/// <summary>
/// The arcade's text: its two fonts, printed and measured a glyph at a time, the high score table's number
/// printer, the rub marker and the mini man lives icon.
/// </summary>
public sealed class ArcadeText
{
    /// <summary>
    /// Index into the large font of the ROM's rub marker — the large-font table's own sprite for
    /// code <c>$5E</c> (notes §116). The initials entry cycles to it to delete a committed letter, and
    /// no other screen prints that character. Its file keeps the sprite editor's name,
    /// <c>Font_L_arrowleft</c>, which is the only "arrow" in the ROM's large font.
    /// </summary>
    public const int RubGlyphIndex = 39;

    private readonly BlitterDraw _blitter;
    private readonly Texture2D[] _fontLarge;
    private readonly Texture2D[] _fontSmall;
    private readonly Texture2D _miniManSprite;

    /// <summary>Prints with these fonts through this blitter.</summary>
    /// <param name="blitter">The blitter that draws each glyph.</param>
    /// <param name="fontLarge">The large font's glyphs.</param>
    /// <param name="fontSmall">The small font's glyphs.</param>
    /// <param name="miniManSprite">The lives icon.</param>
    public ArcadeText(BlitterDraw blitter, Texture2D[] fontLarge, Texture2D[] fontSmall, Texture2D miniManSprite)
    {
        _blitter = blitter;
        _fontLarge = fontLarge;
        _fontSmall = fontSmall;
        _miniManSprite = miniManSprite;
    }

    /// <summary>
    /// Index into the small font (or the large font) for an
    /// ASCII character, or -1 for one the font has no glyph for. The ROM indexes
    /// its font tables by (ASCII - $30) and substitutes its blank glyph for a
    /// space, so the table order is '0'-'9', 'A'-'Z', then '(' ')' (':' and the
    /// arrow exist in the large font only), then the LARGE font's punctuation
    /// '!' ',' '.' '-' (notes §96 — indices 40-43, appended so nothing moves).
    /// </summary>
    public static int GetGlyphIndex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'Z' => 10 + (c - 'A'),
        '(' => 36,
        ')' => 37,
        ':' => 38,
        '!' => 40,
        ',' => 41,
        '.' => 42,
        '-' => 43,
        _ => -1,
    };

    /// <summary>
    /// Draws a string in the arcade's LARGE font (the score digits, the title
    /// screen's "ROBOTRON 2084" / "SAVE THE LAST HUMAN FAMILY", notes §94.1) in
    /// one palette slot, returning the X after the last glyph. Glyphs advance
    /// their width + 1 (the ROM's large-font advance, the same rule the score
    /// uses — <c>HudScoreDigitAdvancePixels</c>); a space advances the same as
    /// the small font's blank; an unknown character is skipped.
    /// </summary>
    public int DrawLargeFontText(SpriteBatch spriteBatch, string text, int x, int y, int slot)
    {
        foreach (char character in text)
        {
            if (character == ' ')
            {
                x += ScreenSize.ToPortPixels(HudLayout.HudSmallFontBlankAdvancePixels);
                continue;
            }

            int index = GetGlyphIndex(character);
            if (index < 0 || index >= _fontLarge.Length)
            {
                continue;
            }

            _blitter.DrawGlyphSlot(spriteBatch, _fontLarge, index, x, y, slot);
            x += ScreenSize.ToPortPixels(_fontLarge[index].Width + HudLayout.HudSmallFontGlyphGapPixels);
        }

        return x;
    }

    /// <summary>The high score table's LARGE-font score (<c>PRSCOR</c> → <c>WRD7V</c>).</summary>
    public int DrawLargeTableNumber(SpriteBatch spriteBatch, int value, int x, int y, int slot) =>
        DrawTableNumber(spriteBatch, _fontLarge, value, x, y, slot);

    /// <summary>
    /// Draws the mini man LIVES icon (notes §58.2) at (x, y), SpecScale x its
    /// 6x8 arcade-pixel size, through the colour-cycle effect so its slot-11
    /// body/arms cycle like the arcade's.
    /// </summary>
    public void DrawMiniMan(SpriteBatch spriteBatch, int x, int y)
    {
        _blitter.UsePassThrough();
        spriteBatch.Draw(
            _miniManSprite,
            new Rectangle(x, y, ScreenSize.ToPortPixels(_miniManSprite.Width), ScreenSize.ToPortPixels(_miniManSprite.Height)),
            Color.White);
    }

    /// <summary>
    /// Draws the arcade's rub marker — the ROM's own <c>$5E</c> glyph — at (x, y) in one palette slot
    /// (notes §116): what the initials entry shows when the letter in the cursor's cell is the delete
    /// marker rather than a character.
    /// </summary>
    public void DrawRubMarker(SpriteBatch spriteBatch, int x, int y, int slot) =>
        _blitter.DrawGlyphSlot(spriteBatch, _fontLarge, RubGlyphIndex, x, y, slot);

    /// <summary>
    /// Draws a string in the arcade's SMALL font (notes §58.3 — the font every
    /// ROM message uses, 4x5 glyphs) in one palette slot, returning the X after
    /// the last glyph. Glyphs advance their width + 1 (ROM $6009); a space
    /// advances 2 px without drawing (the ROM blits its 1-px ':' glyph for a
    /// space); an unknown character is skipped.
    /// </summary>
    public int DrawSmallFontText(SpriteBatch spriteBatch, string text, int x, int y, int slot)
    {
        foreach (char character in text)
        {
            if (character == ' ')
            {
                x += ScreenSize.ToPortPixels(HudLayout.HudSmallFontSpaceAdvancePixels);
                continue;
            }

            int index = GetGlyphIndex(character);
            if (index >= 0 && index < _fontSmall.Length)
            {
                _blitter.DrawGlyphSlot(spriteBatch, _fontSmall, index, x, y, slot);
                x += ScreenSize.ToPortPixels(_fontSmall[index].Width + HudLayout.HudSmallFontGlyphGapPixels);
                continue;
            }

            // The arcade's SMALL font stops at ')' — 38 glyphs: digits, A-Z and the two brackets
            // — so it has no ':' of its own (the ROM's ':' lives in the LARGE font). Rather than
            // drop the character, draw the large font's glyph in its place: that is the arcade's
            // own sprites, and it is why the DEFINE INPUTS page can print "ENTER: SET THE INPUT"
            // (notes §101.11). The colon is one row taller than the capitals, exactly as the two
            // arcade fonts differ.
            if (index >= 0 && index < _fontLarge.Length)
            {
                _blitter.DrawGlyphSlot(spriteBatch, _fontLarge, index, x, y, slot);
                x += ScreenSize.ToPortPixels(_fontLarge[index].Width + HudLayout.HudSmallFontGlyphGapPixels);
            }
        }

        return x;
    }

    /// <summary>The high score table's SMALL-font score (<c>PRSCOR</c> → <c>WRD5V</c>).</summary>
    public int DrawSmallTableNumber(SpriteBatch spriteBatch, int value, int x, int y, int slot) =>
        DrawTableNumber(spriteBatch, _fontSmall, value, x, y, slot);

    /// <summary>
    /// The high score table's number printer (notes §98.5, from a photo
    /// of the arcade): the SIGNIFICANT digits only, packed from the cursor — the
    /// table does NOT use the in-play score display's blanked-leading-zero field
    /// (a row reads "1) DRJ 52127", not "1) DRJ   52127"), which is what makes the
    /// rows' digits line up. Returns the X after the number.
    /// </summary>
    public int DrawTableNumber(SpriteBatch spriteBatch, Texture2D[] glyphs, int value, int x, int y, int slot)
    {
        foreach (ScoreDigit digit in ScoreFormatter.GetDigits(value))
        {
            if (digit.Suppressed)
            {
                continue;
            }

            _blitter.DrawGlyphSlot(spriteBatch, glyphs, digit.Value, x, y, slot);
            x += ScreenSize.ToPortPixels(glyphs[digit.Value].Width + HudLayout.HudSmallFontGlyphGapPixels);
        }

        return x;
    }

    /// <summary>The same measurement for the LARGE font (the score's own advance rule).</summary>
    public int MeasureLargeText(string text) => MeasureText(_fontLarge, text);

    /// <summary>
    /// The width of a string in the arcade's SMALL font, in SPEC pixels: each glyph advances its own
    /// width + 1 (the ROM's $6009 rule) and a space advances the blank's 2 (the ROM blits its 1-px
    /// ':' glyph for a space). The states that CENTRE a line use this rather than assuming a fixed
    /// advance — the glyph widths differ, and the LARGE font's differ from the small one's.
    /// </summary>
    public int MeasureSmallText(string text) => MeasureText(_fontSmall, text);

    private static int MeasureText(Texture2D[] glyphs, string text)
    {
        int width = 0;
        foreach (char character in text)
        {
            if (character == ' ')
            {
                width += HudLayout.HudSmallFontBlankAdvancePixels;
                continue;
            }

            int index = GetGlyphIndex(character);
            if (index >= 0 && index < glyphs.Length)
            {
                width += glyphs[index].Width + HudLayout.HudSmallFontGlyphGapPixels;
            }
        }

        return width;
    }
}
