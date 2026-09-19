using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Hud;

/// <summary>
/// The arcade HUD (notes §58), shared by the playing state, the attract demo
/// (Phase 12.1) and the title screen: in the top band, each player's score at
/// their own column (P1 col 21, P2 col 85) with their spare men as mini man
/// icons immediately to its right (P1 col 46, P2 col 110); at the bottom, the
/// "&lt;c&gt; WAVE" indicator (drawn by <see cref="DrawWaveMessage"/>, not by
/// <see cref="DrawScoresAndMen"/> — the title screen has no wave).
/// </summary>
public static class ArcadeHud
{
    /// <summary>
    /// Scores + spare men for every player in the session. <paramref name="innerBounds"/>
    /// is the playfield's inner rectangle (the wall sits just outside it) so the HUD
    /// row lands eight arcade px above the top wall, exactly as in the ROM.
    /// </summary>
    public static void DrawScoresAndMen(
        SpriteBatch spriteBatch,
        SpriteSet sprites,
        GameSession session,
        Rectangle innerBounds)
    {
        int wallTop = innerBounds.Top - ScreenSize.Scaled(GameplayConstants.WallThicknessSpecPixels);
        int hudY = wallTop - ScreenSize.Scaled(GameplayConstants.HudRowAboveWallPixels);

        foreach (PlayerSlot player in session.Players)
        {
            bool isPlayerOne = player.Number == 1;
            int scoreColumn = isPlayerOne ? GameplayConstants.HudScoreOriginColumnP1 : GameplayConstants.HudScoreOriginColumnP2;
            int menColumn = isPlayerOne ? GameplayConstants.HudMenOriginColumnP1 : GameplayConstants.HudMenOriginColumnP2;

            // ROM $DC13/$DC19: the player whose turn it is blits their score with
            // $AA (slot 10 — one of the colour-CYCLING slots); an idle player's
            // uses $11 (slot 1).
            int slot = ReferenceEquals(player, session.Current)
                ? GameplayConstants.HudScoreSlotCurrent
                : GameplayConstants.HudScoreSlotIdle;

            DrawScore(spriteBatch, sprites, player.Score, GameplayConstants.ArcadeX(scoreColumn * 2), hudY, slot);
            DrawSpareMen(spriteBatch, sprites, player.DisplayedMen, GameplayConstants.ArcadeX(menColumn * 2), hudY);
        }
    }

    /// <summary>
    /// ROM string 104: the wave number in $AA at the BOTTOM of the screen (row
    /// 238, well below the playfield), then +6 px, then " WAVE" in $BB. The ROM
    /// draws it at each wave start and leaves it up while the wave runs.
    /// </summary>
    public static void DrawWaveMessage(SpriteBatch spriteBatch, SpriteSet sprites, int wave)
    {
        int x = GameplayConstants.ArcadeX(GameplayConstants.HudWaveTextColumn * 2);
        int y = GameplayConstants.ArcadeY(GameplayConstants.HudWaveTextRow);
        const int numberSlot = GameplayConstants.HudScoreSlotCurrent;

        if (wave >= 10)
        {
            x = sprites.DrawSmallFontText(spriteBatch, ((wave / 10) % 10).ToString(), x, y, numberSlot);
        }
        else
        {
            // PRINT_BCD_NUMBER with $D1 = 2 (string 104's $15 op): a leading zero
            // advances without drawing — 4 px in the small font.
            x += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
        }

        x = sprites.DrawSmallFontText(spriteBatch, (wave % 10).ToString(), x, y, numberSlot);
        x += ScreenSize.Scaled(GameplayConstants.HudWaveNumberGapPixels);
        sprites.DrawSmallFontText(spriteBatch, " WAVE", x, y, GameplayConstants.HudWaveTextSlot);
    }

    /// <summary>Draws one of the ROM's message strings at its own cursor column/row.</summary>
    public static void DrawMessageText(SpriteBatch spriteBatch, SpriteSet sprites, string text, int arcadeColumn, int arcadeRow, int slot)
    {
        sprites.DrawSmallFontText(
            spriteBatch,
            text,
            GameplayConstants.ArcadeX(arcadeColumn * 2),
            GameplayConstants.ArcadeY(arcadeRow),
            slot);
    }

    /// <summary>
    /// The ROM's title string (128, TITLEM: "ROBOTRON 2084") and its tagline
    /// (129, FAMMM: "SAVE THE LAST HUMAN FAMILY"), both in the LARGE font in
    /// slot $AA. The caller places them; the title screen and the attract
    /// movie's story band each use their own rows (notes §94.1, §96.3).
    /// </summary>
    public static void DrawCenteredLargeText(SpriteBatch spriteBatch, SpriteSet sprites, string text, int y, int slot)
    {
        int width = 0;
        foreach (char character in text)
        {
            if (character == ' ')
            {
                width += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
                continue;
            }

            int index = SpriteSet.GlyphIndex(character);
            if (index < 0 || index >= sprites.FontLarge.Length)
            {
                continue;
            }

            width += ScreenSize.Scaled(sprites.FontLarge[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
        }

        sprites.DrawLargeFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, slot);
    }

    /// <summary>
    /// ROM $DC13 → $6096: the score as seven large-font glyphs at the cursor, a
    /// drawn digit advancing 7 px and a suppressed leading zero 6 px.
    /// </summary>
    private static void DrawScore(SpriteBatch spriteBatch, SpriteSet sprites, int score, int originX, int y, int slot)
    {
        foreach (ScoreFormatter.ScoreGlyph glyph in ScoreFormatter.Layout(
            score,
            originX,
            ScreenSize.Scaled(GameplayConstants.HudScoreDigitAdvancePixels),
            ScreenSize.Scaled(GameplayConstants.HudScoreBlankAdvancePixels)))
        {
            sprites.DrawGlyphSlot(spriteBatch, sprites.FontLarge, glyph.Digit, glyph.X, y, slot);
        }
    }

    /// <summary>ROM $34E0: the spare-man icons, 8 px apart.</summary>
    private static void DrawSpareMen(SpriteBatch spriteBatch, SpriteSet sprites, int count, int originX, int y)
    {
        int pitch = ScreenSize.Scaled(GameplayConstants.HudMenPitchPixels);

        for (int i = 0; i < count; i++)
        {
            sprites.DrawMiniMan(spriteBatch, originX + (i * pitch), y);
        }
    }
}
