using Robotron2084.Core;

namespace Robotron2084.Tuning;

/// <summary>Where the arcade's HUD and on-screen messages sit, and the arcade-to-port coordinate mapping.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>DRAW_PLAYER_SCORES</c> (<c>$DC13</c>, called by <c>$34AF</c>) and
///             <c>DRAW_LIVES_REMAINING</c> (<c>$34E0</c>), and the string table at <c>$6291</c>
///         </item>
///     </list>
///     The arcade screen is 304 by 256 pixels: pair-major, 4 bits a pixel, 152 two-pixel columns (<c>mame-notes</c>).
///     Arcade coordinates map to the port screen by screen proportion, in integer arithmetic.
///     <para>
///         The score for player 1 has its cursor at column 21 (x 42) on row 14, and player 2's at column 85 (x 170) on
///         row 14 (<c>LEAX -$300,X</c> from the <c>$180E</c> and <c>$580E</c> blit destinations). The four BCD score
///         bytes are eight digit places, left to right: 10M (which the arcade masks off but the port draws), 1M, 100k,
///         10k, 1k, 100, 10 and 1. A drawn glyph advances by its width plus 1. A suppressed leading zero advances less
///         (<c>$6128</c>: 4 pixels, and 2 more for the large font).
///     </para>
///     <para>
///         The spare lives sit on row 14, 8 pixels apart, drawn from the 6 by 8 spare-life sprite (<c>$3592</c> and
///         <c>$3596</c>), at most seven. In the arcade the first is at column 46 (x 92) for player 1 and column 110
///         (x 220) for player 2.
///     </para>
///     <para>
///         The current player's score is drawn in a colour-cycling palette index (<c>$AA</c>, the LF process) and
///         everyone else's in another (<c>$11</c>). The wave message "n  WAVE" is string 104, in the small font, at
///         column 62 on row 238 at the bottom of the screen: the number in <c>$AA</c> and " WAVE" in <c>$BB</c>.
///     </para>
///     <para>
///         The small font is the font every arcade message uses (notes §58.3): glyphs 4 by 5 pixels that advance by their
///         width plus 1 (<c>$6009</c>). A suppressed leading zero advances 4 pixels (<c>$6128</c>; the 2 added at
///         <c>$6136</c> is for the large font only). A space is the ROM's 1-pixel colon glyph, so it advances 2 pixels.
///     </para>
///     <para>
///         The messages are ROM strings 103, 75, 40 and 104, with their control codes decoded (notes §58.3).
///     </para>
/// </remarks>
public static class HudLayout
{
    /// <summary>How tall the arcade screen is, in arcade pixels.</summary>
    public const int ArcadeScreenHeight = 256;

    /// <summary>How wide the arcade screen is, in arcade pixels.</summary>
    public const int ArcadeScreenWidth = 304;

    /// <summary>The column of the "GAME OVER" message, in arcade pixels.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLEND</c>, string 40 (<c>GOMP</c>), cursor <c>$3E80</c>.</remarks>
    public const int GameOverMessageColumn = 62;

    /// <summary>The row of the "GAME OVER" message, in arcade pixels.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLEND</c>, string 40 (<c>GOMP</c>), cursor <c>$3E80</c>.</remarks>
    public const int GameOverMessageRow = 128;

    /// <summary>The most spare lives the HUD shows.</summary>
    /// <remarks>Original source: <c>MANDSV</c>, "MAX OF 7".</remarks>
    public const int HudMaxSpareLives = 7;

    /// <summary>The arcade byte column of the first spare life of player 1.</summary>
    /// <remarks>
    ///     The arcade's column is 46, right behind a seven-digit score. An eight-digit score is 7 pixels longer, so the
    ///     port moves both rows of spare-life icons 4 columns (8 pixels) to the right (notes §139).
    /// </remarks>
    public const int HudLivesOriginColumnP1 = 50;

    /// <summary>The arcade byte column of the first spare life of player 2.</summary>
    /// <remarks>The arcade's column is 110. See <see cref="HudLivesOriginColumnP1" />.</remarks>
    public const int HudLivesOriginColumnP2 = 114;

    /// <summary>How far apart the spare life icons are, in arcade pixels.</summary>
    /// <remarks>Disassembly: <c>DRAW_LIVES_REMAINING</c> (<c>$34E0</c>), <c>ADDA #$04</c> at <c>$3506</c>, which is 4 byte columns.</remarks>
    public const int HudLivesPitchPixels = 8;

    /// <summary>How tall a spare life icon is, in rows.</summary>
    /// <remarks>Disassembly: the spare-life sprite metadata at <c>$3592</c>, which gives 8 rows.</remarks>
    public const int HudSpareLifeHeightPixels = 8;

    /// <summary>How wide a spare life icon is, in arcade pixels.</summary>
    /// <remarks>Disassembly: the spare-life sprite metadata at <c>$3592</c>, which gives 3 bytes.</remarks>
    public const int HudSpareLifeWidthPixels = 6;

    /// <summary>How far above the top of the wall the HUD is drawn, in arcade pixels.</summary>
    /// <remarks>
    ///     The ROM draws the whole HUD on row 14 (<c>col*256 + 14</c>) while the top wall sits on row 22, which is eight
    ///     rows above the wall, so the 8-row spare-life icons finish on row 21, exactly next to the wall. The port gets
    ///     its HUD row the same way (the wall's top minus this many arcade pixels) and not from the screen proportion of
    ///     <see cref="ToPortY" />. The port's wall comes from the spec's margin and is not at the arcade's 22 of 256
    ///     height, so the 12-pixel score glyphs and 16-pixel icons would otherwise run through the wall band.
    /// </remarks>
    public const int HudRowAboveWallPixels = 8;

    /// <summary>How far the cursor moves on past a suppressed leading zero of a score, in arcade pixels.</summary>
    /// <remarks>Disassembly: <c>$6128</c> and <c>$6136</c>, which are 4 and then 2 more for the large font.</remarks>
    public const int HudScoreBlankAdvancePixels = 6;

    /// <summary>How far the cursor moves on past a drawn digit of a score, in arcade pixels.</summary>
    /// <remarks>Disassembly: <c>$6009</c>, the glyph width plus 1.</remarks>
    public const int HudScoreDigitAdvancePixels = 7;

    /// <summary>The arcade byte column of player 1's score.</summary>
    public const int HudScoreOriginColumnP1 = 21;

    /// <summary>The arcade byte column of player 2's score, which is player 1's plus 64.</summary>
    /// <remarks>Original source: <c>P2ORG</c> minus <c>P1ORG</c>.</remarks>
    public const int HudScoreOriginColumnP2 = 85;

    /// <summary>The palette index of the current player's score.</summary>
    /// <remarks>Disassembly: <c>DRAW_PLAYER_SCORES</c>, <c>$DC19</c>: <c>$AA</c>, the LF process.</remarks>
    public const int HudScorePaletteIndexCurrent = 10;

    /// <summary>The palette index of a score that is not the current player's.</summary>
    /// <remarks>Disassembly: <c>DRAW_PLAYER_SCORES</c> (<c>$DC13</c>): <c>$11</c>.</remarks>
    public const int HudScorePaletteIndexIdle = 1;

    /// <summary>How far the cursor moves on past a suppressed leading zero in the small font, in arcade pixels.</summary>
    /// <remarks>Disassembly: <c>$6128</c>. The 2 added at <c>$6136</c> is for the large font only.</remarks>
    public const int HudSmallFontBlankAdvancePixels = 4;

    /// <summary>The gap between one small font glyph and the next, in arcade pixels.</summary>
    /// <remarks>Disassembly: <c>$6009</c>, where a glyph advances by its width plus 1.</remarks>
    public const int HudSmallFontGlyphGapPixels = 1;

    /// <summary>How far the cursor moves on past a space in the small font, in arcade pixels.</summary>
    /// <remarks>A space is the ROM's 1-pixel colon glyph, so it advances 2 pixels.</remarks>
    public const int HudSmallFontSpaceAdvancePixels = 2;

    /// <summary>The gap between the wave number and the word WAVE, in arcade pixels.</summary>
    /// <remarks>Original source: string 104, <c>MOVE_CURSOR_REL +$03</c>, which is 3 bytes.</remarks>
    public const int HudWaveNumberGapPixels = 6;

    /// <summary>The column of the wave message, in arcade pixels.</summary>
    /// <remarks>Original source: string 104, cursor <c>$3EEE</c>.</remarks>
    public const int HudWaveTextColumn = 62;

    /// <summary>The row of the wave message, in arcade pixels.</summary>
    /// <remarks>Original source: string 104, cursor <c>$3EEE</c>.</remarks>
    public const int HudWaveTextRow = 238;

    /// <summary>The palette index of the wave message.</summary>
    /// <remarks>Original source: string 104, colour <c>$BB</c>.</remarks>
    public const int HudWaveTextPaletteIndex = 0x0B;

    /// <summary>The column of the PAUSE banner, in arcade pixels.</summary>
    /// <remarks>
    ///     Port only (notes §101). The banner sits on the same centre line the ROM's own messages use, so it looks like
    ///     one of them.
    /// </remarks>
    public const int PausedMessageColumn = 62;

    /// <summary>The row of the PAUSE banner, in arcade pixels.</summary>
    /// <remarks>Port only (notes §101). See <see cref="PausedMessageColumn" />.</remarks>
    public const int PausedMessageRow = 120;

    /// <summary>The row of the "PLAYER n GAME OVER" message, in arcade pixels.</summary>
    /// <remarks>Original source: string 75, cursor <c>$3E86</c>.</remarks>
    public const int PlayerGameOverMessageRow = 134;

    /// <summary>The column of the "PLAYER n" message at a turn start, in arcade pixels.</summary>
    /// <remarks>Original source: string 103, cursor <c>$3F7A</c>.</remarks>
    public const int PlayerTurnMessageColumn = 63;

    /// <summary>The row of the "PLAYER n" message at a turn start, in arcade pixels.</summary>
    /// <remarks>Original source: string 103, cursor <c>$3F7A</c>.</remarks>
    public const int PlayerTurnMessageRow = 122;

    /// <summary>How far apart the lines of the title's F-key menu are, in arcade pixels.</summary>
    /// <remarks>Port only: the title's F-key menu has no arcade counterpart.</remarks>
    public const int TitleOptionRowStepPixels = 12;

    /// <summary>
    ///     An arcade COLUMN to the port screen's x — a column is <see cref="ScreenSize.ArcadePixelsPerByte" /> arcade
    ///     pixels.
    /// </summary>
    public static int ToPortColumnX(int column)
    {
        return ToPortX(column * ScreenSize.ArcadePixelsPerByte);
    }

    /// <summary>Arcade screen x (of 304) mapped to the port screen (proportional, integer math).</summary>
    public static int ToPortX(int arcadePx)
    {
        return arcadePx * ScreenSize.Width / ArcadeScreenWidth;
    }

    /// <summary>Arcade screen y (of 256) mapped to the port screen (proportional, integer math).</summary>
    public static int ToPortY(int arcadePx)
    {
        return arcadePx * ScreenSize.Height / ArcadeScreenHeight;
    }
}
