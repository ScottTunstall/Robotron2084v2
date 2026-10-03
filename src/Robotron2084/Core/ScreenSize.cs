namespace Robotron2084.Core;

/// <summary>
/// Port playfield geometry — the single source of truth for resolution. "Port" means this C# version of the
/// game, as opposed to the original arcade machine, so a <b>port pixel</b> is one dot of the 640 by 400 screen
/// this game draws (see docs/glossary.md).
/// spec.txt's 320x200 coordinate space is widened by <see cref="SpecScale"/>
/// for sharper rendering; every spec-pixel size, speed, and position in the
/// codebase goes through <see cref="ToPortPixels(int)"/> (or derives from
/// <see cref="Width"/>/<see cref="Height"/>) in one place, so raising the
/// resolution is a change to the constants below and nothing else:
/// <list type="bullet">
/// <item>
/// <see cref="SpecScale"/> — internal pixels per spec pixel. Raise it
/// (2 → 3, 4…) to render the SAME layout sharper: the render target, window
/// integer scaling, sprite draw sizes, and all gameplay geometry follow
/// automatically.
/// </item>
/// <item>
/// <see cref="SpecWidth"/>/<see cref="SpecHeight"/> — the game's layout
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

    /// <summary>Height of the screen the game draws, in port pixels (SpecHeight × SpecScale).</summary>
    public const int Height = SpecHeight * SpecScale;

    /// <summary>Spec.txt playfield height in original pixels.</summary>
    public const int SpecHeight = 200;

    /// <summary>Port pixels in one spec pixel (the render scale of the spec's 320x200 space).</summary>
    public const int SpecScale = 2;

    /// <summary>Spec.txt playfield width in original pixels.</summary>
    public const int SpecWidth = 320;

    /// <summary>Bits of a 16-bit ROM coordinate below the whole pixel (the low byte).</summary>
    public const int SubpixelBits = 8;

    /// <summary>Subpixels in one pixel: the ROM's 16-bit coordinates keep the pixel in the high byte, so 1/256 of a pixel is the smallest step.</summary>
    public const int SubpixelsPerPixel = 1 << SubpixelBits;

    /// <summary>Width of the screen the game draws, in port pixels (SpecWidth × SpecScale).</summary>
    public const int Width = SpecWidth * SpecScale;

    /// <summary>ROM columns to port pixels: a column is two arcade pixels.</summary>
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
