using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Rendering;

/// <summary>
/// Builds every sprite at runtime as a pixel-array <see cref="Texture2D"/> —
/// no Content pipeline assets needed. Entity silhouettes are
/// original art authored fresh for this project (simple symmetric shapes on
/// a transparent background, in the spec's colour per entity); the four
/// player laser pictures are ROM art (R5 $35BE-$35DC) built at arcade-pixel
/// dimensions. Entity patterns are authored on a fixed
/// <see cref="DesignSize"/>×<see cref="DesignSize"/> design canvas and
/// nearest-neighbour scaled to <see cref="PatternSize"/>, so they stay
/// correct at any <c>ScreenSize.SpecScale</c>.
/// </summary>
public sealed class PixelArtFactory
{
    /// <summary>
    /// Canvas every hand-authored pattern is written on (2x arcade pixels in
    /// the original 2x design). Patterns are scaled to <see cref="PatternSize"/>
    /// before use, so the authoring coordinates never change when the
    /// resolution changes.
    /// </summary>
    private const int DesignSize = 32;

    /// <summary>Runtime pattern size: the 16 spec-px entity box × SpecScale (32x32 at 2x).</summary>
    private static readonly int PatternSize = ScreenSize.Scaled(GameplayConstants.EntitySizeSpecPixels);

    private readonly GraphicsDevice _device;

    public PixelArtFactory(GraphicsDevice device)
    {
        _device = device;
    }

    /// <summary>The single flat texture primitive: a solid rectangle of one colour.</summary>
    public Texture2D CreateSolid(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        pixels.AsSpan().Fill(color);
        return Create(width, height, pixels);
    }

    /// <summary>Creates a texture from an explicit pixel array (row-major).</summary>
    public Texture2D Create(int width, int height, Color[] pixels)
    {
        Texture2D texture = new(_device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    // ---- ROM laser art (player laser pictures; R5 $35BE-$35DC = old source
    //      RRG23 LLPC/ULPC/DLLPC/ULLPC, author ROM-verified 2026-09-13) ----
    // 4 bits per pixel, high nibble = left pixel. Authored at arcade-pixel
    // dimensions (1 art pixel = 1 texture pixel); SpriteSet.DrawSprite scales
    // them by SpecScale at draw time.

    /// <summary>LLPC ($35BE, 3 bytes × 1 row = 6×1): a solid bar — LEFT and RIGHT.</summary>
    public static Color[] BuildLaserBarPattern(Color color)
    {
        Color[] pattern = new Color[6];
        pattern.AsSpan().Fill(color);
        return pattern;
    }

    /// <summary>ULPC ($35C1, 1 byte × 6 rows = 2×6): the left column lit — UP and DOWN.</summary>
    public static Color[] BuildLaserColumnPattern(Color color)
    {
        Color[] pattern = new Color[2 * LaserDiagonalSize];
        for (int row = 0; row < LaserDiagonalSize; row++)
        {
            pattern[row * 2] = color; // left pixel (the ROM high nibble)
        }

        return pattern;
    }

    /// <summary>
    /// ULLPC ($35D9, 3 bytes × 6 rows = 6×6): the main diagonal
    /// (top-left → bottom-right) — UP-LEFT and DOWN-RIGHT.
    /// </summary>
    public static Color[] BuildLaserDiagonalMainPattern(Color color)
    {
        Color[] pattern = new Color[LaserDiagonalSize * LaserDiagonalSize];
        for (int i = 0; i < LaserDiagonalSize; i++)
        {
            pattern[i * LaserDiagonalSize + i] = color;
        }

        return pattern;
    }

    /// <summary>
    /// DLLPC ($35C7, 3 bytes × 6 rows = 6×6): the anti-diagonal
    /// (top-right → bottom-left) — DOWN-LEFT and UP-RIGHT.
    /// </summary>
    public static Color[] BuildLaserDiagonalAntiPattern(Color color)
    {
        Color[] pattern = new Color[LaserDiagonalSize * LaserDiagonalSize];
        for (int i = 0; i < LaserDiagonalSize; i++)
        {
            pattern[i * LaserDiagonalSize + (LaserDiagonalSize - 1 - i)] = color;
        }

        return pattern;
    }

    private const int LaserDiagonalSize = 6;

    // ---- Hand-authored 32x32 silhouette patterns (one method per shape) ----

    /// <summary>Humanoid: head + torso + two arms + two legs.</summary>
    public static Color[] BuildPlayerPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        FillRect(canvas, 12, 2, 8, 8, color);    // head
        FillRect(canvas, 10, 10, 12, 12, color); // torso
        FillRect(canvas, 5, 11, 3, 8, color); // left arm
        FillRect(canvas, 24, 11, 3, 8, color); // right arm
        FillRect(canvas, 11, 22, 5, 9, color); // left leg
        FillRect(canvas, 16, 22, 5, 9, color); // right leg
        return ScalePattern(canvas);
    }

    /// <summary>Spiked tower: top ball, stem, diamond midsection, base.</summary>
    public static Color[] BuildElectrodePattern(Color color)
    {
        Color[] canvas = NewCanvas();
        FillRect(canvas, 13, 3, 6, 5, color);  // top ball
        FillRect(canvas, 15, 8, 2, 9, color);  // stem
        for (int dy = -5; dy <= 5; dy++)       // diamond midsection
        {
            int width = (5 - Math.Abs(dy)) * 3 + 2;
            FillRect(canvas, 16 - width / 2, 22 + dy, width, 1, color);
        }

        FillRect(canvas, 11, 28, 10, 3, color); // base
        return ScalePattern(canvas);
    }

    /// <summary>Small blocky robot: head, torso, arms, legs.</summary>
    public static Color[] BuildGruntPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        FillRect(canvas, 12, 5, 8, 6, color);   // head
        FillRect(canvas, 9, 11, 14, 10, color); // torso
        FillRect(canvas, 4, 12, 3, 8, color);   // left arm
        FillRect(canvas, 25, 12, 3, 8, color);  // right arm
        FillRect(canvas, 11, 21, 4, 9, color);  // left leg
        FillRect(canvas, 17, 21, 4, 9, color);  // right leg
        return ScalePattern(canvas);
    }

    /// <summary>Bulky robot: wide head, shoulders, torso, short legs.</summary>
    public static Color[] BuildHulkPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        FillRect(canvas, 11, 3, 10, 8, color);  // head
        FillRect(canvas, 4, 11, 24, 6, color);  // shoulders
        FillRect(canvas, 8, 17, 16, 9, color);  // torso
        FillRect(canvas, 8, 26, 6, 5, color);   // left leg
        FillRect(canvas, 18, 26, 6, 5, color);  // right leg
        return ScalePattern(canvas);
    }

    /// <summary>Orb: filled disc.</summary>
    public static Color[] BuildSpheroidPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        for (int y = 0; y < DesignSize; y++)
        {
            for (int x = 0; x < DesignSize; x++)
            {
                int dx = x - 15;
                int dy = y - 15;
                if (dx * dx + dy * dy <= 13 * 13)
                {
                    canvas[y * DesignSize + x] = color;
                }
            }
        }

        return ScalePattern(canvas);
    }

    /// <summary>Four-legged angular stalker: core + corner legs + mid nubs.</summary>
    public static Color[] BuildEnforcerPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        FillRect(canvas, 11, 11, 10, 10, color); // core
        FillRect(canvas, 4, 4, 5, 5, color);     // corner legs
        FillRect(canvas, 23, 4, 5, 5, color);
        FillRect(canvas, 4, 23, 5, 5, color);
        FillRect(canvas, 23, 23, 5, 5, color);
        FillRect(canvas, 15, 2, 2, 3, color);    // mid nubs
        FillRect(canvas, 15, 27, 2, 3, color);
        FillRect(canvas, 2, 15, 3, 2, color);
        FillRect(canvas, 27, 15, 3, 2, color);
        return ScalePattern(canvas);
    }

    /// <summary>Angular gem: filled diamond.</summary>
    public static Color[] BuildQuarkPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        for (int y = 0; y < DesignSize; y++)
        {
            for (int x = 0; x < DesignSize; x++)
            {
                int dx = x - 15;
                int dy = y - 15;
                if (Math.Abs(dx) + Math.Abs(dy) <= 13)
                {
                    canvas[y * DesignSize + x] = color;
                }
            }
        }

        return ScalePattern(canvas);
    }

    /// <summary>Turret + barrel, hull, treads.</summary>
    public static Color[] BuildTankPattern(Color color)
    {
        Color[] canvas = NewCanvas();
        FillRect(canvas, 12, 8, 8, 7, color);   // turret
        FillRect(canvas, 14, 2, 3, 6, color);   // barrel
        FillRect(canvas, 5, 15, 22, 9, color);  // hull
        FillRect(canvas, 4, 24, 24, 6, color);  // treads
        return ScalePattern(canvas);
    }

    private static Color[] NewCanvas() => new Color[DesignSize * DesignSize];

    private static void FillRect(Color[] canvas, int x, int y, int width, int height, Color color)
    {
        for (int row = Math.Max(0, y); row < Math.Min(DesignSize, y + height); row++)
        {
            for (int col = Math.Max(0, x); col < Math.Min(DesignSize, x + width); col++)
            {
                canvas[row * DesignSize + col] = color;
            }
        }
    }

    /// <summary>Nearest-neighbour scales a DesignSize×DesignSize pattern to PatternSize×PatternSize.</summary>
    private static Color[] ScalePattern(Color[] design)
    {
        if (PatternSize == DesignSize)
        {
            return design;
        }

        Color[] output = new Color[PatternSize * PatternSize];
        for (int y = 0; y < PatternSize; y++)
        {
            int sy = y * DesignSize / PatternSize;
            for (int x = 0; x < PatternSize; x++)
            {
                int sx = x * DesignSize / PatternSize;
                output[y * PatternSize + x] = design[sy * DesignSize + sx];
            }
        }

        return output;
    }
}
