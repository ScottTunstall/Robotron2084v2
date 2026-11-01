using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Hud;

/// <summary>
///     The arcade HUD (notes §58), shared by the playing state, the attract demo
///     and the title screen: in the top band, each player's score at
///     their own column (P1 col 21, P2 col 85) with their spare lives as spare-life
///     icons immediately to its right (P1 col 46, P2 col 110); at the bottom, the
///     "&lt;c&gt; WAVE" indicator (drawn by <see cref="DrawWaveMessage" />, not by
///     <see cref="DrawScoresAndLives" /> — the title screen has no wave).
/// </summary>
public static class ArcadeHud
{
    /// <summary>
    ///     The ROM's title string (128, TITLEM: "ROBOTRON 2084") and its tagline
    ///     (129, FAMMM: "SAVE THE LAST HUMAN FAMILY"), both in the LARGE font in
    ///     palette index $AA. The caller places them; the title screen and the attract
    ///     movie's story band each use their own rows (notes §94.1, §96.3).
    /// </summary>
    public static void DrawCentredLargeText(SpriteBatch spriteBatch, TextureSet textureSet, string text, int y, int paletteIndex)
    {
        var width = 0;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                width += ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudSmallFontBlankAdvancePixels);
                continue;
            }

            var index = ArcadeText.GetGlyphIndex(character);
            if (index < 0 || index >= textureSet.FontLargeTextures.Length) continue;

            width += ScreenSize.ToPortPixelsFromArcadePixels(textureSet.FontLargeTextures[index].Width +
                                                             HudLayout.HudSmallFontGlyphGapPixels);
        }

        textureSet.ArcadeText.DrawLargeFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, paletteIndex);
    }

    /// <summary>Draws one of the ROM's message strings at its own cursor column/row.</summary>
    public static void DrawMessageText(SpriteBatch spriteBatch, TextureSet textureSet, string text, int arcadeColumn,
        int arcadeRow, int paletteIndex)
    {
        textureSet.ArcadeText.DrawSmallFontText(
            spriteBatch,
            text,
            HudLayout.ToPortX(arcadeColumn * 2),
            HudLayout.ToPortY(arcadeRow),
            paletteIndex);
    }

    /// <summary>Draws each player's score and spare lives along the top of the screen, just above the wall.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="textureSet">The texture set, which holds the text and the spare-life icons.</param>
    /// <param name="gameSession">The game being played, which says who the players are.</param>
    /// <param name="innerBounds">
    ///     The play area inside the wall. The row is placed a set distance above its top edge, as in the
    ///     arcade.
    /// </param>
    /// <param name="isSpareLivesVisible">False while the demo plays: the phony player's spare lives are not shown.</param>
    public static void DrawScoresAndLives(
        SpriteBatch spriteBatch,
        TextureSet textureSet,
        GameSession gameSession,
        Rectangle innerBounds,
        bool isSpareLivesVisible = true)
    {
        var hudY = GetScoresAndLivesRowY(innerBounds);

        foreach (var playerSlot in gameSession.PlayerSlots)
        {
            var isPlayerOne = playerSlot.Number == 1;
            var scoreColumn = HudLayout.HudScoreOriginColumnP2;
            var livesColumn = HudLayout.HudLivesOriginColumnP2;
            if (isPlayerOne)
            {
                scoreColumn = HudLayout.HudScoreOriginColumnP1;
                livesColumn = HudLayout.HudLivesOriginColumnP1;
            }

            // ROM $DC13/$DC19: the player whose turn it is blits their score with
            // $AA (palette index 10 — one of the colour-CYCLING palette indices); an idle player's
            // uses $11 (palette index 1).
            var paletteIndex = HudLayout.HudScorePaletteIndexIdle;
            var isCurrentPlayer = ReferenceEquals(playerSlot, gameSession.GetCurrent());
            if (isCurrentPlayer) paletteIndex = HudLayout.HudScorePaletteIndexCurrent;

            DrawScore(spriteBatch, textureSet, playerSlot.Score, HudLayout.ToPortX(scoreColumn * 2), hudY, paletteIndex);
            if (isSpareLivesVisible)
                DrawSpareLives(spriteBatch, textureSet, playerSlot.GetDisplayedSpareLives(), HudLayout.ToPortX(livesColumn * 2), hudY);
        }
    }

    /// <summary>
    ///     ROM string 104: the wave number in $AA at the BOTTOM of the screen (row
    ///     238, well below the playfield), then +6 px, then " WAVE" in $BB. The ROM
    ///     draws it at each wave start and leaves it up while the wave runs.
    /// </summary>
    public static void DrawWaveMessage(SpriteBatch spriteBatch, TextureSet textureSet, int wave)
    {
        var x = HudLayout.ToPortX(HudLayout.HudWaveTextColumn * 2);
        var y = HudLayout.ToPortY(HudLayout.HudWaveTextRow);
        const int numberPaletteIndex = HudLayout.HudScorePaletteIndexCurrent;

        if (wave >= 10)
            x = textureSet.ArcadeText.DrawSmallFontText(spriteBatch, (wave / 10 % 10).ToString(), x, y, numberPaletteIndex);
        else
            // PRINT_BCD_NUMBER with $D1 = 2 (string 104's $15 op): a leading zero
            // advances without drawing — 4 px in the small font.
            x += ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudSmallFontBlankAdvancePixels);

        x = textureSet.ArcadeText.DrawSmallFontText(spriteBatch, (wave % 10).ToString(), x, y, numberPaletteIndex);
        x += ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudWaveNumberGapPixels);
        textureSet.ArcadeText.DrawSmallFontText(spriteBatch, " WAVE", x, y, HudLayout.HudWaveTextPaletteIndex);
    }

    /// <summary>
    ///     The canvas row the scores and the spare-life icons are drawn at: the top wall less the ROM's
    ///     eight arcade pixels. It is negative — and the whole HUD invisible — when
    ///     <paramref name="innerBounds" /> runs to the top of the canvas.
    /// </summary>
    internal static int GetScoresAndLivesRowY(Rectangle innerBounds)
    {
        return innerBounds.Top - ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.WallThicknessArcadePixels) -
               ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudRowAboveWallPixels);
    }

    /// <summary>
    ///     ROM $DC13 → $6096: the score as seven large-font glyphs at the cursor, a
    ///     drawn digit advancing 7 px and a suppressed leading zero 6 px.
    /// </summary>
    private static void DrawScore(SpriteBatch spriteBatch, TextureSet textureSet, int score, int originX, int y, int paletteIndex)
    {
        foreach (var glyph in ScoreFormatter.LayOutGlyphs(
                     score,
                     originX,
                     ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudScoreDigitAdvancePixels),
                     ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudScoreBlankAdvancePixels)))
            textureSet.BlitterDraw.DrawGlyphPaletteIndex(spriteBatch, textureSet.FontLargeTextures, glyph.Digit, glyph.X, y, paletteIndex);
    }

    /// <summary>ROM $34E0: the spare-life icons, 8 px apart.</summary>
    private static void DrawSpareLives(SpriteBatch spriteBatch, TextureSet textureSet, int count, int originX, int y)
    {
        var pitch = ScreenSize.ToPortPixelsFromArcadePixels(HudLayout.HudLivesPitchPixels);

        for (var i = 0; i < count; i++) textureSet.ArcadeText.DrawSpareLife(spriteBatch, originX + i * pitch, y);
    }
}
