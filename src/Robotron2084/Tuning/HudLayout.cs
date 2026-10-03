using Robotron2084.Core;

namespace Robotron2084.Tuning;

/// <summary>Where the arcade's HUD and on-screen messages sit, and the arcade-to-port coordinate mapping.</summary>
public static class HudLayout
{
    public const int ArcadeScreenHeight = 256;

    // The arcade screen is 304x256 (mame-notes: pair-major 4bpp, 152 two-px
    // columns). Arcade coordinates map to the 640x400 port screen by screen
    // proportion (integer math).
    public const int ArcadeScreenWidth = 304;

    public const int GameOverMessageColumn = 62;

    public const int GameOverMessageRow = 128;

    public const int HudMaxMen = 7;

    // The arcade's columns were 46 and 110, right behind a seven-digit score. An eight-digit score is 7 pixels longer, so the port moves both rows of men 4 columns (8 pixels) to the right (notes §139).
    public const int HudMenOriginColumnP1 = 50;

    public const int HudMenOriginColumnP2 = 114;

    public const int HudMenPitchPixels = 8;

    public const int HudMiniManHeightPixels = 8;

    public const int HudMiniManWidthPixels = 6;

    /// <summary>
    /// The ROM draws the whole HUD on row 14 (<c>col*256 + 14</c>) while the top wall
    /// sits on row 22, i.e. EIGHT rows above the wall — and the 8-row-tall mini men
    /// therefore finish on row 21, exactly adjacent to the wall. The port derives its
    /// HUD row the same way (wall top minus this many arcade pixels) instead of using
    /// <see cref="ArcadeY"/>'s screen proportion, because the port's wall comes from
    /// spec.txt's margin and is not at the arcade's 22/256 height: at ArcadeY(14) = 21
    /// the 12-px score glyphs and 16-px men ran through the 32..40 wall band, which is
    /// exactly the round-8 complaint ("the score also overlaps the border wall").
    /// </summary>
    public const int HudRowAboveWallPixels = 8;

    public const int HudScoreBlankAdvancePixels = 6;

    public const int HudScoreDigitAdvancePixels = 7;

    // The HUD is the arcade's, from DRAW_PLAYER_SCORES ($DC13, called by
    // $34AF), DRAW_LIVES_REMAINING ($34E0) and the $6291 string table:
    //
    //   score   P1 cursor = col 21 (x 42), row 14; P2 = col 85 (x 170), row 14
    //           (`LEAX -$300,X` from the $180E/$580E blit destination)
    //   digits  the 4 BCD score bytes as EIGHT positions, left to right:
    //           10M (masked off by the arcade; the port draws it), 1M, 100k, 10k, 1k, 100, 10, 1
    //           a drawn glyph advances width+1 = 7 px; a suppressed leading
    //           zero advances 6 px ($6128: 4 px, then +2 for the large font)
    //   men     P1 col 46 (x 92), P2 col 110 (x 220) in the arcade; the port: 50 and 114. Row 14, 8 px apart,
    //           the 6x8 mini man sprite ($3592/$3596), capped at SEVEN
    //   colour  the CURRENT player's score blits in slot 10 ($AA — a CYCLING
    //           slot: the LF process); everyone else's in slot 1 ($11)
    //   wave    "<n>  WAVE" at col 62 / row 238 (the BOTTOM), string 104,
    //           small font, number in $AA and " WAVE" in $BB
    //
    // The ROM's HUD row is 14, which keeps the score clear of the port's 40-px
    // wall band.
    public const int HudScoreOriginColumnP1 = 21;     // arcade byte column

    public const int HudScoreOriginColumnP2 = 85;     // = P1 + 64 (P2ORG - P1ORG)
                                                      // ROM MANDSV: "MAX OF 7"

    // ROM $3506: ADDA #$04 (byte cols)

    // ROM $3592 metadata (3 bytes)

    // ROM $3592 metadata (8 rows)

    // ROM $6009: width + 1

    // ROM $6128 + $6136 (4 + 2)

    public const int HudScoreSlotCurrent = 10;        // ROM $DC19: $AA, the LF process

    public const int HudScoreSlotIdle = 1;            // ROM $DC13: $11

    public const int HudSmallFontBlankAdvancePixels = 4;

    // Small-font metrics (the font every arcade MESSAGE uses — notes §58.3):
    // 4x5 glyphs that advance width+1 = 5 px ($6009); a suppressed leading zero
    // advances 4 px ($6128, the $6136 +2 is large-font only); a space is the
    // ROM's 1-px ':' glyph, so it advances 2 px.
    public const int HudSmallFontGlyphGapPixels = 1;

    public const int HudSmallFontSpaceAdvancePixels = 2;

    // Messages (ROM strings 103/75/40/104 — control codes decoded, notes §58.3).
    public const int HudWaveNumberGapPixels = 6;

    public const int HudWaveTextColumn = 62;          // ROM string 104: cursor $3EEE

    public const int HudWaveTextRow = 238;
    // string 104: MOVE_CURSOR_REL +$03

    public const int HudWaveTextSlot = 0x0B;          // string 104: colour $BB

    // string 40 / 75: cursor $3E80/$3E86
    // Port-only (notes §101): the PAUSE banner, on the same centre line the ROM's own
    // messages use, so it looks like one of them.
    public const int PausedMessageColumn = 62;

    public const int PausedMessageRow = 120;
    public const int PlayerGameOverMessageRow = 134;
    public const int PlayerTurnMessageColumn = 63;    // string 103: cursor $3F7A

    public const int PlayerTurnMessageRow = 122;

    // string 75: cursor $3F79 (the name)
    // Port-only: the title's F-key menu, and how far apart its lines sit.
    public const int TitleOptionRowStepPixels = 12;

    /// <summary>An arcade COLUMN to the port screen's x — a column is <see cref="ScreenSize.ArcadePixelsPerColumn"/> arcade pixels.</summary>
    public static int ToPortColumnX(int column) => ToPortX(column * ScreenSize.ArcadePixelsPerColumn);

    /// <summary>Arcade screen x (of 304) mapped to the port screen (proportional, integer math).</summary>
    public static int ToPortX(int arcadePx) => arcadePx * ScreenSize.Width / ArcadeScreenWidth;

    /// <summary>Arcade screen y (of 256) mapped to the port screen (proportional, integer math).</summary>
    public static int ToPortY(int arcadePx) => arcadePx * ScreenSize.Height / ArcadeScreenHeight;
}
