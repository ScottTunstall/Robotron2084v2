using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Graphics;

/// <summary>
///     Builds every sprite at runtime as a pixel-array <see cref="Texture2D" /> —
///     no Content pipeline assets needed. Entity silhouettes are
///     authored fresh for this project (simple symmetric shapes on
///     a transparent background, in the spec's colour per entity); the four
///     player laser sprites come from the ROM (R5 $35BE-$35DC) and are built at arcade-pixel
///     dimensions. Entity patterns are authored on a fixed
///     <see cref="DesignSize" />×<see cref="DesignSize" /> design canvas and
///     nearest-neighbour scaled to <see cref="PatternSize" />, so they stay
///     correct at any <c>ScreenSize.PortPixelsPerArcadePixel</c>.
/// </summary>
public sealed class TextureFactory
{
    /// <summary>
    ///     Canvas every hand-authored pattern is written on (2x arcade pixels in
    ///     the original 2x design). Patterns are scaled to <see cref="PatternSize" />
    ///     before use, so the authoring coordinates never change when the
    ///     resolution changes.
    /// </summary>
    private const int DesignSize = 32;

    private const int LaserDiagonalSize = 6;

    /// <summary>Runtime pattern size: the 16 arcade pixel entity box × PortPixelsPerArcadePixel (32x32 at 2x).</summary>
    private static readonly int PatternSize =
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EntitySizeArcadePixels);

    private readonly GraphicsDevice _graphicsDevice;

    public TextureFactory(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>Spiked tower: top ball, stem, diamond midsection, base.</summary>
    public static Color[] BuildElectrodePattern(Color colour)
    {
        var canvas = CreateCanvas();
        FillRectangle(canvas, 13, 3, 6, 5, colour); // top ball
        FillRectangle(canvas, 15, 8, 2, 9, colour); // stem
        for (var dy = -5; dy <= 5; dy++) // diamond midsection
        {
            var width = (5 - Math.Abs(dy)) * 3 + 2;
            FillRectangle(canvas, 16 - width / 2, 22 + dy, width, 1, colour);
        }

        FillRectangle(canvas, 11, 28, 10, 3, colour); // base
        return ScalePattern(canvas);
    }

    /// <summary>Four-legged angular stalker: core + corner legs + mid nubs.</summary>
    public static Color[] BuildEnforcerPattern(Color colour)
    {
        var canvas = CreateCanvas();
        FillRectangle(canvas, 11, 11, 10, 10, colour); // core
        FillRectangle(canvas, 4, 4, 5, 5, colour); // corner legs
        FillRectangle(canvas, 23, 4, 5, 5, colour);
        FillRectangle(canvas, 4, 23, 5, 5, colour);
        FillRectangle(canvas, 23, 23, 5, 5, colour);
        FillRectangle(canvas, 15, 2, 2, 3, colour); // mid nubs
        FillRectangle(canvas, 15, 27, 2, 3, colour);
        FillRectangle(canvas, 2, 15, 3, 2, colour);
        FillRectangle(canvas, 27, 15, 3, 2, colour);
        return ScalePattern(canvas);
    }

    /// <summary>Small blocky robot: head, torso, arms, legs.</summary>
    public static Color[] BuildGruntPattern(Color colour)
    {
        var canvas = CreateCanvas();
        FillRectangle(canvas, 12, 5, 8, 6, colour); // head
        FillRectangle(canvas, 9, 11, 14, 10, colour); // torso
        FillRectangle(canvas, 4, 12, 3, 8, colour); // left arm
        FillRectangle(canvas, 25, 12, 3, 8, colour); // right arm
        FillRectangle(canvas, 11, 21, 4, 9, colour); // left leg
        FillRectangle(canvas, 17, 21, 4, 9, colour); // right leg
        return ScalePattern(canvas);
    }

    /// <summary>Bulky robot: wide head, shoulders, torso, short legs.</summary>
    public static Color[] BuildHulkPattern(Color colour)
    {
        var canvas = CreateCanvas();
        FillRectangle(canvas, 11, 3, 10, 8, colour); // head
        FillRectangle(canvas, 4, 11, 24, 6, colour); // shoulders
        FillRectangle(canvas, 8, 17, 16, 9, colour); // torso
        FillRectangle(canvas, 8, 26, 6, 5, colour); // left leg
        FillRectangle(canvas, 18, 26, 6, 5, colour); // right leg
        return ScalePattern(canvas);
    }

    /// <summary>LLPC ($35BE, 3 bytes × 1 row = 6×1): a solid bar — LEFT and RIGHT.</summary>
    public static Color[] BuildLaserBarPattern(Color colour)
    {
        var pattern = new Color[6];
        pattern.AsSpan().Fill(colour);
        return pattern;
    }

    // ---- ROM laser sprites (player laser sprites; R5 $35BE-$35DC = old source
    //      RRG23 LLPC/ULPC/DLLPC/ULLPC, author ROM-verified 2026-09-13) ----
    // 4 bits per pixel, high nibble = left pixel. Authored at arcade-pixel
    // dimensions (1 arcade pixel = 1 texture pixel); BlitterDraw.DrawSprite scales
    // them by PortPixelsPerArcadePixel at draw time.
    /// <summary>ULPC ($35C1, 1 byte × 6 rows = 2×6): the left column lit — UP and DOWN.</summary>
    public static Color[] BuildLaserColumnPattern(Color colour)
    {
        var pattern = new Color[2 * LaserDiagonalSize];
        for (var row = 0; row < LaserDiagonalSize; row++) pattern[row * 2] = colour; // left pixel (the ROM high nibble)

        return pattern;
    }

    /// <summary>
    ///     DLLPC ($35C7, 3 bytes × 6 rows = 6×6): the anti-diagonal
    ///     (top-right → bottom-left) — DOWN-LEFT and UP-RIGHT.
    /// </summary>
    public static Color[] BuildLaserDiagonalAntiPattern(Color colour)
    {
        var pattern = new Color[LaserDiagonalSize * LaserDiagonalSize];
        for (var i = 0; i < LaserDiagonalSize; i++)
            pattern[i * LaserDiagonalSize + (LaserDiagonalSize - 1 - i)] = colour;

        return pattern;
    }

    /// <summary>
    ///     ULLPC ($35D9, 3 bytes × 6 rows = 6×6): the main diagonal
    ///     (top-left → bottom-right) — UP-LEFT and DOWN-RIGHT.
    /// </summary>
    public static Color[] BuildLaserDiagonalMainPattern(Color colour)
    {
        var pattern = new Color[LaserDiagonalSize * LaserDiagonalSize];
        for (var i = 0; i < LaserDiagonalSize; i++) pattern[i * LaserDiagonalSize + i] = colour;

        return pattern;
    }

    /// <summary>Humanoid: head + torso + two arms + two legs.</summary>
    public static Color[] BuildPlayerPattern(Color colour)
    {
        var canvas = CreateCanvas();
        FillRectangle(canvas, 12, 2, 8, 8, colour); // head
        FillRectangle(canvas, 10, 10, 12, 12, colour); // torso
        FillRectangle(canvas, 5, 11, 3, 8, colour); // left arm
        FillRectangle(canvas, 24, 11, 3, 8, colour); // right arm
        FillRectangle(canvas, 11, 22, 5, 9, colour); // left leg
        FillRectangle(canvas, 16, 22, 5, 9, colour); // right leg
        return ScalePattern(canvas);
    }

    /// <summary>Angular gem: filled diamond.</summary>
    public static Color[] BuildQuarkPattern(Color colour)
    {
        var canvas = CreateCanvas();
        for (var y = 0; y < DesignSize; y++)
            for (var x = 0; x < DesignSize; x++)
            {
                var dx = x - 15;
                var dy = y - 15;
                if (Math.Abs(dx) + Math.Abs(dy) <= 13) canvas[y * DesignSize + x] = colour;
            }

        return ScalePattern(canvas);
    }

    // ---- Hand-authored 32x32 silhouette patterns (one method per shape) ----
    /// <summary>Orb: filled disc.</summary>
    public static Color[] BuildSpheroidPattern(Color colour)
    {
        var canvas = CreateCanvas();
        for (var y = 0; y < DesignSize; y++)
            for (var x = 0; x < DesignSize; x++)
            {
                var dx = x - 15;
                var dy = y - 15;
                if (dx * dx + dy * dy <= 13 * 13) canvas[y * DesignSize + x] = colour;
            }

        return ScalePattern(canvas);
    }

    /// <summary>Turret + barrel, hull, treads.</summary>
    public static Color[] BuildTankPattern(Color colour)
    {
        var canvas = CreateCanvas();
        FillRectangle(canvas, 12, 8, 8, 7, colour); // turret
        FillRectangle(canvas, 14, 2, 3, 6, colour); // barrel
        FillRectangle(canvas, 5, 15, 22, 9, colour); // hull
        FillRectangle(canvas, 4, 24, 24, 6, colour); // treads
        return ScalePattern(canvas);
    }

    /// <summary>Creates a texture from an explicit pixel array (row-major).</summary>
    public Texture2D Create(int width, int height, Color[] pixels)
    {
        Texture2D texture = new(_graphicsDevice, width, height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>The single flat texture primitive: a solid rectangle of one colour.</summary>
    public Texture2D CreateSolid(int width, int height, Color colour)
    {
        var pixels = new Color[width * height];
        pixels.AsSpan().Fill(colour);
        return Create(width, height, pixels);
    }

    private static Color[] CreateCanvas()
    {
        return new Color[DesignSize * DesignSize];
    }

    private static void FillRectangle(Color[] canvas, int x, int y, int width, int height, Color colour)
    {
        for (var row = Math.Max(0, y); row < Math.Min(DesignSize, y + height); row++)
            for (var col = Math.Max(0, x); col < Math.Min(DesignSize, x + width); col++)
                canvas[row * DesignSize + col] = colour;
    }

    /// <summary>Nearest-neighbour scales a DesignSize×DesignSize pattern to PatternSize×PatternSize.</summary>
    private static Color[] ScalePattern(Color[] design)
    {
        if (PatternSize == DesignSize) return design;

        var output = new Color[PatternSize * PatternSize];
        for (var y = 0; y < PatternSize; y++)
        {
            var sourceY = y * DesignSize / PatternSize;
            for (var x = 0; x < PatternSize; x++)
            {
                var sourceX = x * DesignSize / PatternSize;
                output[y * PatternSize + x] = design[sourceY * DesignSize + sourceX];
            }
        }

        return output;
    }
}
