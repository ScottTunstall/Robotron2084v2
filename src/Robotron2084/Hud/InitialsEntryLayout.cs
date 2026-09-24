using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Hud;

/// <summary>
/// The CONG page's cursors, colours and cell geometry (notes §116). The initials screen is RRET.ASM's
/// message 95 (<c>CONGP</c>), so every position here is one of its control codes; cursors are the
/// ROM's (column, row) units and a column is two arcade pixels.
/// </summary>
/// <remarks>
/// <c>CONGP</c> prints "PLAYER n" at <c>CURSAB $40,$10</c> = (64, 16), "YOU ARE A ROBOTRON HERO" at
/// (41, 48), and "ENTER YOUR INITIALS:" at (45, 88) — all in the LARGE font (the <c>WRD7V</c>
/// default) in <c>COLOR $44</c> = slot 4. <c>TELSUB</c> then switches to the SMALL font and white
/// (<c>SFONT,COLOR $99</c>) for the two instructions at (47, 192) and (50, 204). The letters are typed
/// into the echo region <c>$4680</c> = (column 70, row 128), the screen's centre, and <c>G0SUB</c>'s
/// "frob" markers — the raw video byte <c>$99</c>, a two-pixel dash — sit eight rows below the echo
/// cursor.
/// </remarks>
public static class InitialsEntryLayout
{
    // ---- CONGP's three large-font lines ------------------------------------------
    public const int PlayerColumn = 64;
    public const int PlayerRow = 16;
    public const int HeroColumn = 41;
    public const int HeroRow = 48;
    public const int PromptColumn = 45;
    public const int PromptRow = 88;

    // ---- TELSUB's two small-font instructions ------------------------------------
    public const int SelectColumn = 47;
    public const int SelectRow = 192;
    public const int FireColumn = 50;
    public const int FireRow = 204;

    /// <summary>CONGP's <c>COLOR $44</c>: the page's ink, slot 4 for the large-font lines.</summary>
    public const int InkSlot = 4;

    /// <summary>TELSUB's <c>COLOR $99</c>: white for the instructions and the frob markers.</summary>
    public const int InstructionSlot = 9;

    // ---- the echo region and its cells -------------------------------------------
    /// <summary>The echo region's column (ROM <c>$4680</c> = column 70, row 128).</summary>
    public const int EchoColumn = 70;

    /// <summary>The echo region's row (ROM <c>$4680</c> = column 70, row 128).</summary>
    public const int EchoRow = 128;

    /// <summary>
    /// One cell's width: the port's own large-font advance — a glyph plus the ROM's one-pixel gap
    /// (<c>$6009</c>, the HUD's 7 arcade pixels). The ROM advances its echo pointer one COLUMN (two
    /// pixels) per letter, which cannot hold a six-pixel glyph, so the port spaces the cells by an
    /// advance wide enough to print one — the deviation notes §116 records.
    /// </summary>
    public static int CellAdvancePixels => ScreenSize.Scaled(GameplayConstants.HudScoreDigitAdvancePixels);

    /// <summary>Rows below the echo cursor of <c>G0SUB</c>'s frob marker (`STA 8,X`).</summary>
    public const int MarkerRowOffset = 8;

    /// <summary>The marker's width: the raw video byte <c>$99</c> lights both pixels of one column.</summary>
    public static int MarkerWidthPixels => GameplayConstants.ArcadeX(GameplayConstants.ArcadePixelsPerColumn);

    /// <summary>The X of cell <paramref name="index"/> (0-based), in port pixels.</summary>
    public static int CellX(int index) => GameplayConstants.ArcadeColumnX(EchoColumn) + (index * CellAdvancePixels);

    /// <summary>The Y the three letters are drawn at, in port pixels.</summary>
    public static int EchoY => GameplayConstants.ArcadeY(EchoRow);

    /// <summary>The Y of the row the frob markers sit on, in port pixels.</summary>
    public static int MarkerY => GameplayConstants.ArcadeY(EchoRow + MarkerRowOffset);

    /// <summary>The height of one arcade row on the port's canvas — the marker is a single row.</summary>
    public static int MarkerHeightPixels => GameplayConstants.ArcadeY(EchoRow + MarkerRowOffset + 1) - MarkerY;
}
