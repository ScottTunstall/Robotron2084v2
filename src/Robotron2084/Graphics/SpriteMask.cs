using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Graphics;

/// <summary>
/// A sprite's opaque pixels, one flag per pixel in the sprite's own grid — the shape a pixel-perfect
/// contact test compares (notes §118).
/// </summary>
/// <remarks>
/// ROM: the collision routine at $D85C walks two objects' sprite bytes and treats a zero byte as
/// transparent — <c>LDA B,U / BEQ $D88C</c>, the disassembly's own comment being "if 0 then consider
/// these pixels as transparent, do not use in collision detection". It works a BYTE (two pixels) at a
/// time because the arcade's screen buffer is four bits per pixel; the port keeps the transparency rule
/// and drops the packing, as plan.md requires (4bpp storage is not modelled), so one cell here is one
/// sprite pixel.
/// </remarks>
public sealed class SpriteMask
{
    private readonly bool[] _opaquePixels;

    private SpriteMask(int width, int height, bool[] opaquePixels)
    {
        Width = width;
        Height = height;
        _opaquePixels = opaquePixels;
    }

    /// <summary>The sprite's height in pixels.</summary>
    public int Height { get; }

    /// <summary>The sprite's width in pixels.</summary>
    public int Width { get; }

    /// <summary>Builds a mask from the pixels that are set — the way a test states a shape by hand.</summary>
    /// <param name="width">The sprite's width in pixels.</param>
    /// <param name="height">The sprite's height in pixels.</param>
    /// <param name="opaquePixels">The opaque pixels, in the sprite's own grid.</param>
    public static SpriteMask CreateFromPixels(int width, int height, IEnumerable<(int X, int Y)> opaquePixels)
    {
        var isOpaque = new bool[width * height];
        foreach ((int x, int y) in opaquePixels)
        {
            isOpaque[(y * width) + x] = true;
        }

        return new SpriteMask(width, height, isOpaque);
    }

    /// <summary>Makes a mask from a sprite: every pixel that is not see-through counts as solid.</summary>
    /// <param name="texture">The sprite the mask is made for, which is usually one animation frame of a character.</param>
    public static SpriteMask CreateFromTexture(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        var isOpaque = new bool[pixels.Length];
        for (int pixel = 0; pixel < pixels.Length; pixel++)
        {
            isOpaque[pixel] = pixels[pixel].A != 0;
        }

        return new SpriteMask(texture.Width, texture.Height, isOpaque);
    }

    /// <summary>
    /// True when the two sprites cover a common screen pixel. Each mask is placed by the rectangle its
    /// sprite is DRAWN in — <see cref="BlitterDraw.DrawnRect"/> is the one definition of that
    /// placement, so the collision follows the sprite wherever the drawer puts it — and one sprite pixel
    /// covers a square of screen pixels (the render scale).
    ///
    /// The cheap test comes first and the pixel walk only ever runs over the band the two sprites share.
    /// The DRAWN rectangles are the right thing to reject on, not the collision boxes: several sprites are
    /// drawn larger than the box that centres them (the spark is 8x7 sprite in a 4x4 box, the laser 6x6 in the
    /// same, the tank's birth frames bigger still), so a box-only rejection would drop the near misses the
    /// arcade counts.
    /// </summary>
    /// <param name="a">The first sprite's mask.</param>
    /// <param name="aDrawnBounds">The rectangle <paramref name="a"/> is drawn in, in screen pixels.</param>
    /// <param name="b">The second sprite's mask.</param>
    /// <param name="bDrawnBounds">The rectangle <paramref name="b"/> is drawn in, in screen pixels.</param>
    public static bool Overlaps(SpriteMask a, Rectangle aDrawnBounds, SpriteMask b, Rectangle bDrawnBounds)
    {
        (int left, int top, int right, int bottom) = GetSharedScreenRect(aDrawnBounds, bDrawnBounds);
        if (left >= right || top >= bottom)
        {
            return false;
        }

        int cell = aDrawnBounds.Width / a.Width;
        for (int y = top; y < bottom; y++)
        {
            int rowA = (y - aDrawnBounds.Y) / cell;
            int rowB = (y - bDrawnBounds.Y) / cell;
            for (int x = left; x < right; x++)
            {
                if (a.IsOpaque((x - aDrawnBounds.X) / cell, rowA) && b.IsOpaque((x - bDrawnBounds.X) / cell, rowB))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>True when the sprite has an opaque pixel at (x, y); false outside the sprite.</summary>
    /// <param name="x">Pixel column, from the sprite's left edge.</param>
    /// <param name="y">Pixel row, from the sprite's top edge.</param>
    public bool IsOpaque(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height && _opaquePixels[(y * Width) + x];

    /// <summary>The screen pixels both drawn rectangles cover, or an empty rectangle when they miss.</summary>
    private static (int Left, int Top, int Right, int Bottom) GetSharedScreenRect(Rectangle a, Rectangle b) =>
        (Math.Max(a.Left, b.Left), Math.Max(a.Top, b.Top), Math.Min(a.Right, b.Right), Math.Min(a.Bottom, b.Bottom));
}
