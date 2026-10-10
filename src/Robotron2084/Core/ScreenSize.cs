namespace Robotron2084.Core;

/// <summary>
/// Port playfield geometry — the single source of truth for resolution. "Port" means this C# version of the
/// game, as opposed to the original arcade machine, so a <b>port pixel</b> is one dot of the 640 by 400 screen
/// this game draws (see docs/glossary.md).
/// The game's screen is <see cref="WidthInArcadePixels"/> by <see cref="HeightInArcadePixels"/> arcade pixels. It is made
/// bigger by <see cref="SpecScale"/> for sharper rendering; every size, speed, and position in the
/// codebase goes through <see cref="ToPortPixels(int)"/> (or derives from
/// <see cref="Width"/>/<see cref="Height"/>) in one place, so raising the
/// resolution is a change to the constants below and nothing else:
/// <list type="bullet">
/// <item>
/// <see cref="SpecScale"/> — port pixels per arcade pixel. Raise it
/// (2 → 3, 4…) to render the SAME layout sharper: the render target, window
/// integer scaling, sprite draw sizes, and all gameplay geometry follow
/// automatically.
/// </item>
/// <item>
/// <see cref="WidthInArcadePixels"/>/<see cref="HeightInArcadePixels"/> — the game's layout
/// itself. Changing them changes how much playfield the game has (a design
/// decision, not just a resolution bump).
/// </item>
/// </list>
/// Pure and unit-testable.
/// </summary>
public static class ScreenSize
{
    /// <summary>Arcade (ROM) pixels in one ROM column: the video buffer is addressed as
    /// <c>column * 256 + row</c> at 4bpp, so a column is two arcade pixels (notes §113).</summary>
    public const int ArcadePixelsPerColumn = 2;

    /// <summary>How tall the screen the game draws is, in port pixels. It is <see cref="HeightInArcadePixels"/> times <see cref="SpecScale"/>.</summary>
    public const int Height = HeightInArcadePixels * SpecScale;

    /// <summary>How tall the game's screen is, in arcade pixels, before it is made bigger by <see cref="SpecScale"/>.</summary>
    public const int HeightInArcadePixels = 200;

    /// <summary>How many port pixels wide and tall one arcade pixel is drawn. Raise it to draw the same screen sharper.</summary>
    public const int SpecScale = 2;

    /// <summary>Bits of a 16-bit ROM coordinate below the whole pixel (the low byte).</summary>
    public const int SubpixelBits = 8;

    /// <summary>Subpixels in one pixel: the ROM's 16-bit coordinates keep the pixel in the high byte, so 1/256 of a pixel is the smallest step.</summary>
    public const int SubpixelsPerPixel = 1 << SubpixelBits;

    /// <summary>How wide the screen the game draws is, in port pixels. It is <see cref="WidthInArcadePixels"/> times <see cref="SpecScale"/>.</summary>
    public const int Width = WidthInArcadePixels * SpecScale;

    /// <summary>How wide the game's screen is, in arcade pixels, before it is made bigger by <see cref="SpecScale"/>.</summary>
    public const int WidthInArcadePixels = 320;

    /// <summary>Turns the gap between two points, given in port pixels, into the ROM's own count: columns sideways plus rows up and down.</summary>
    /// <param name="from">One point, in port pixels.</param>
    /// <param name="to">The other point, in port pixels.</param>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>GETHTG</c>, "SUM OF ABS VALUES DX,DY". A sideways gap counts half what the same gap in pixels would, because a column is two arcade pixels (notes §113). Disassembly: <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>).</remarks>
    public static int ToColumnAndRowDistance(IntVector2 from, IntVector2 to) =>
        (Math.Abs(to.X - from.X) / ToPortPixelsFromColumns(1)) + (Math.Abs(to.Y - from.Y) / ToPortPixels(1));

    /// <summary>Changes a number of the arcade's columns into port pixels. A column is <see cref="ArcadePixelsPerColumn"/> arcade pixels wide, and an arcade pixel is <see cref="SpecScale"/> port pixels wide, so one column is 4 port pixels.</summary>
    /// <param name="columns">How many columns.</param>
    public static int ToPortPixelsFromColumns(int columns) => ToPortPixels(columns * ArcadePixelsPerColumn);

    /// <summary>
    /// Largest integer scale at which the playfield fits in the given
    /// available area. Never downscales (minimum 1x).
    /// </summary>
    public static int ComputeMaxIntegerScale(int availableWidth, int availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 1;
        }

        int scale = Math.Min(availableWidth / Width, availableHeight / Height);
        return Math.Max(1, scale);
    }

    /// <summary>Converts a length in arcade pixels or spec pixels to port pixels. The two are the same size, so one method serves both.</summary>
    /// <param name="pixels">The length in arcade pixels or spec pixels.</param>
    public static int ToPortPixels(int pixels) => pixels * SpecScale;
}
