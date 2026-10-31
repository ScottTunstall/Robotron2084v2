using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Tuning;

namespace Robotron2084.Graphics;

/// <summary>
///     The arcade's text: its two fonts, printed and measured a glyph at a time, the high score table's number
///     printer, the rub marker and the spare-life icon.
/// </summary>
public sealed class ArcadeText
{
    /// <summary>
    ///     Index into the large font of the ROM's rub marker — the large-font table's own sprite for
    ///     code <c>$5E</c> (notes §116). The initials entry cycles to it to delete a committed letter, and
    ///     no other screen prints that character. Its file keeps the sprite editor's name,
    ///     <c>Font_L_arrowleft</c>, which is the only "arrow" in the ROM's large font.
    /// </summary>
    public const int RubGlyphIndex = 39;

    private readonly BlitterDraw _blitterDraw;
    private readonly Texture2D[] _fontLargeTextures;
    private readonly Texture2D[] _fontSmallTextures;
    private readonly Texture2D _spareLifeTexture;

    /// <summary>Prints with these fonts through this blitter.</summary>
    /// <param name="blitterDraw">The blitter that draws each glyph.</param>
    /// <param name="fontLargeTextures">The large font's glyphs.</param>
    /// <param name="fontSmallTextures">The small font's glyphs.</param>
    /// <param name="spareLifeTexture">The lives icon.</param>
    public ArcadeText(BlitterDraw blitterDraw, Texture2D[] fontLargeTextures, Texture2D[] fontSmallTextures, Texture2D spareLifeTexture)
    {
        _blitterDraw = blitterDraw;
        _fontLargeTextures = fontLargeTextures;
        _fontSmallTextures = fontSmallTextures;
        _spareLifeTexture = spareLifeTexture;
    }

    /// <summary>
    ///     Index into the small font (or the large font) for an
    ///     ASCII character, or -1 for one the font has no glyph for. The ROM indexes
    ///     its font tables by (ASCII - $30) and substitutes its blank glyph for a
    ///     space, so the table order is '0'-'9', 'A'-'Z', then '(' ')' (':' and the
    ///     arrow exist in the large font only), then the LARGE font's punctuation
    ///     '!' ',' '.' '-' (notes §96 — indices 40-43, appended so nothing moves).
    /// </summary>
    public static int GetGlyphIndex(char c)
    {
        return c switch
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
            _ => -1
        };
    }

    /// <summary>
    ///     Draws a string in the arcade's LARGE font (the score digits, the title
    ///     screen's "ROBOTRON 2084" / "SAVE THE LAST HUMAN FAMILY", notes §94.1) in
    ///     one palette index, returning the X after the last glyph. Glyphs advance
    ///     their width + 1 (the ROM's large-font advance, the same rule the score
    ///     uses — <c>HudScoreDigitAdvancePixels</c>); a space advances the same as
    ///     the small font's blank; an unknown character is skipped.
    /// </summary>
    public int DrawLargeFontText(SpriteBatch spriteBatch, string text, int x, int y, int paletteIndex)
    {
        foreach (var character in text)
        {
            if (character == ' ')
            {
                x += ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudSmallFontBlankAdvancePixels);
                continue;
            }

            var index = GetGlyphIndex(character);
            if (index < 0 || index >= _fontLargeTextures.Length) continue;

            _blitterDraw.DrawGlyphPaletteIndex(spriteBatch, _fontLargeTextures, index, x, y, paletteIndex);
            x += ScreenSize.ToPortPixelsFromArcadePixels(_fontLargeTextures[index].Width +
                                                         HudLayout.HudSmallFontGlyphGapPixels);
        }

        return x;
    }

    /// <summary>The high score table's LARGE-font score (<c>PRSCOR</c> → <c>WRD7V</c>).</summary>
    public int DrawLargeTableNumber(SpriteBatch spriteBatch, int value, int x, int y, int paletteIndex)
    {
        return DrawTableNumber(spriteBatch, _fontLargeTextures, value, x, y, paletteIndex);
    }

    /// <summary>
    ///     Draws the spare-life (LIVES) icon (notes §58.2) at (x, y), PortPixelsPerArcadePixel x its
    ///     6x8 arcade-pixel size, through the colour-cycle effect so its palette index 11
    ///     body/arms cycle like the arcade's.
    /// </summary>
    public void DrawSpareLife(SpriteBatch spriteBatch, int x, int y)
    {
        _blitterDraw.UsePassThrough();
        spriteBatch.Draw(
            _spareLifeTexture,
            new Rectangle(x, y, ScreenSize.ToPortPixelsFromArcadePixels(_spareLifeTexture.Width),
                ScreenSize.ToPortPixelsFromArcadePixels(_spareLifeTexture.Height)),
            Color.White);
    }

    /// <summary>
    ///     Draws the arcade's rub marker — the ROM's own <c>$5E</c> glyph — at (x, y) in one palette index
    ///     (notes §116): what the initials entry shows when the letter in the cursor's cell is the delete
    ///     marker rather than a character.
    /// </summary>
    public void DrawRubMarker(SpriteBatch spriteBatch, int x, int y, int paletteIndex)
    {
        _blitterDraw.DrawGlyphPaletteIndex(spriteBatch, _fontLargeTextures, RubGlyphIndex, x, y, paletteIndex);
    }

    /// <summary>
    ///     Draws a string in the arcade's SMALL font (notes §58.3 — the font every
    ///     ROM message uses, 4x5 glyphs) in one palette index, returning the X after
    ///     the last glyph. Glyphs advance their width + 1 (ROM $6009); a space
    ///     advances 2 px without drawing (the ROM blits its 1-px ':' glyph for a
    ///     space); an unknown character is skipped.
    /// </summary>
    public int DrawSmallFontText(SpriteBatch spriteBatch, string text, int x, int y, int paletteIndex)
    {
        foreach (var character in text)
        {
            if (character == ' ')
            {
                x += ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudSmallFontSpaceAdvancePixels);
                continue;
            }

            var index = GetGlyphIndex(character);
            if (index >= 0 && index < _fontSmallTextures.Length)
            {
                _blitterDraw.DrawGlyphPaletteIndex(spriteBatch, _fontSmallTextures, index, x, y, paletteIndex);
                x += ScreenSize.ToPortPixelsFromArcadePixels(_fontSmallTextures[index].Width +
                                                             HudLayout.HudSmallFontGlyphGapPixels);
                continue;
            }

            // The arcade's SMALL font stops at ')' — 38 glyphs: digits, A-Z and the two brackets
            // — so it has no ':' of its own (the ROM's ':' lives in the LARGE font). Rather than
            // drop the character, draw the large font's glyph in its place: that is the arcade's
            // own sprites, and it is why the DEFINE INPUTS page can print "ENTER: SET THE INPUT"
            // (notes §101.11). The colon is one row taller than the capitals, exactly as the two
            // arcade fonts differ.
            if (index >= 0 && index < _fontLargeTextures.Length)
            {
                _blitterDraw.DrawGlyphPaletteIndex(spriteBatch, _fontLargeTextures, index, x, y, paletteIndex);
                x += ScreenSize.ToPortPixelsFromArcadePixels(_fontLargeTextures[index].Width +
                                                             HudLayout.HudSmallFontGlyphGapPixels);
            }
        }

        return x;
    }

    /// <summary>The high score table's SMALL-font score (<c>PRSCOR</c> → <c>WRD5V</c>).</summary>
    public int DrawSmallTableNumber(SpriteBatch spriteBatch, int value, int x, int y, int paletteIndex)
    {
        return DrawTableNumber(spriteBatch, _fontSmallTextures, value, x, y, paletteIndex);
    }

    /// <summary>
    ///     The high score table's number printer (notes §98.5, from a photo
    ///     of the arcade): the SIGNIFICANT digits only, packed from the cursor — the
    ///     table does NOT use the in-play score display's blanked-leading-zero field
    ///     (a row reads "1) DRJ 52127", not "1) DRJ   52127"), which is what makes the
    ///     rows' digits line up. Returns the X after the number.
    /// </summary>
    public int DrawTableNumber(SpriteBatch spriteBatch, Texture2D[] glyphTextures, int value, int x, int y, int paletteIndex)
    {
        foreach (var digit in ScoreFormatter.GetDigits(value))
        {
            if (digit.Suppressed) continue;

            _blitterDraw.DrawGlyphPaletteIndex(spriteBatch, glyphTextures, digit.Value, x, y, paletteIndex);
            x += ScreenSize.ToPortPixelsFromArcadePixels(glyphTextures[digit.Value].Width +
                                                         HudLayout.HudSmallFontGlyphGapPixels);
        }

        return x;
    }

    /// <summary>The same measurement for the LARGE font (the score's own advance rule).</summary>
    public int MeasureLargeText(string text)
    {
        return MeasureText(_fontLargeTextures, text);
    }

    /// <summary>
    ///     The width of a string in the arcade's SMALL font, in arcade pixels: each glyph advances its own
    ///     width + 1 (the ROM's $6009 rule) and a space advances the blank's 2 (the ROM blits its 1-px
    ///     ':' glyph for a space). The states that CENTRE a line use this rather than assuming a fixed
    ///     advance — the glyph widths differ, and the LARGE font's differ from the small one's.
    /// </summary>
    public int MeasureSmallText(string text)
    {
        return MeasureText(_fontSmallTextures, text);
    }

    private static int MeasureText(Texture2D[] glyphTextures, string text)
    {
        var width = 0;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                width += HudLayout.HudSmallFontBlankAdvancePixels;
                continue;
            }

            var index = GetGlyphIndex(character);
            if (index >= 0 && index < glyphTextures.Length)
                width += glyphTextures[index].Width + HudLayout.HudSmallFontGlyphGapPixels;
        }

        return width;
    }
}
