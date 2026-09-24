namespace Robotron2084.Core;

/// <summary>
/// Internal playfield geometry — the single source of truth for resolution.
/// spec.txt's 320x200 coordinate space is widened by <see cref="SpecScale"/>
/// for sharper rendering; every spec-pixel size, speed, and position in the
/// codebase goes through <see cref="Scaled(int)"/> (or derives from
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
    /// <summary>Internal pixels per spec pixel (render scale of the spec's 320x200 space).</summary>
    public const int SpecScale = 2;

    /// <summary>Spec.txt playfield width in original pixels.</summary>
    public const int SpecWidth = 320;

    /// <summary>Spec.txt playfield height in original pixels.</summary>
    public const int SpecHeight = 200;

    /// <summary>Arcade (ROM) pixels in one ROM column: the video buffer is addressed as
    /// <c>column * 256 + row</c> at 4bpp, so a column is two arcade pixels (notes §113).</summary>
    public const int ArcadePixelsPerColumn = 2;

    /// <summary>Internal render width (SpecWidth × SpecScale — 640 at 2x).</summary>
    public const int Width = SpecWidth * SpecScale;

    /// <summary>Internal render height (SpecHeight × SpecScale — 400 at 2x).</summary>
    public const int Height = SpecHeight * SpecScale;

    /// <summary>Converts a spec.txt pixel value to internal pixels.</summary>
    public static int Scaled(int specPixels) => specPixels * SpecScale;

    /// <summary>Arcade (ROM) pixels to port pixels on the playfield: one arcade pixel is one spec pixel.</summary>
    public static int ArcadePixels(int arcadePixels) => Scaled(arcadePixels);

    /// <summary>ROM columns to port pixels: a column is two arcade pixels.</summary>
    public static int Columns(int columns) => Scaled(columns * ArcadePixelsPerColumn);

    /// <summary>
    /// Largest integer scale at which the playfield fits in the given
    /// available area. Never downscales (minimum 1x).
    /// </summary>
    public static int MaxIntegerScale(int availableWidth, int availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 1;
        }

        int scale = Math.Min(availableWidth / Width, availableHeight / Height);
        return Math.Max(1, scale);
    }
}
