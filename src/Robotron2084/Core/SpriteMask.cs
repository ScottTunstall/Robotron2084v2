using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Core;

/// <summary>
/// A picture's opaque pixels, one flag per pixel in the picture's own grid — the shape a pixel-perfect
/// contact test compares (notes §118).
/// </summary>
/// <remarks>
/// ROM: the collision routine at $D85C walks two objects' picture bytes and treats a zero byte as
/// transparent — <c>LDA B,U / BEQ $D88C</c>, the disassembly's own comment being "if 0 then consider
/// these pixels as transparent, do not use in collision detection". It works a BYTE (two pixels) at a
/// time because the arcade's screen buffer is four bits per pixel; the port keeps the transparency rule
/// and drops the packing, as plan.md requires (4bpp storage is not modelled), so one cell here is one
/// PICTURE pixel.
/// </remarks>
public sealed class SpriteMask
{
    private readonly bool[] _opaque;

    private SpriteMask(int width, int height, bool[] opaque)
    {
        Width = width;
        Height = height;
        _opaque = opaque;
    }

    /// <summary>The picture's width in pixels.</summary>
    public int Width { get; }

    /// <summary>The picture's height in pixels.</summary>
    public int Height { get; }

    /// <summary>True when the picture has an opaque pixel at (x, y); false outside the picture.</summary>
    /// <param name="x">Pixel column, from the picture's left edge.</param>
    /// <param name="y">Pixel row, from the picture's top edge.</param>
    public bool IsOpaque(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height && _opaque[(y * Width) + x];

    /// <summary>Derives a mask from a picture's alpha channel: any non-transparent pixel is opaque.</summary>
    /// <param name="picture">The texture the mask is built for (usually a ROM frame).</param>
    public static SpriteMask FromTexture(Texture2D picture)
    {
        var pixels = new Color[picture.Width * picture.Height];
        picture.GetData(pixels);

        var opaque = new bool[pixels.Length];
        for (int pixel = 0; pixel < pixels.Length; pixel++)
        {
            opaque[pixel] = pixels[pixel].A != 0;
        }

        return new SpriteMask(picture.Width, picture.Height, opaque);
    }

    /// <summary>Builds a mask from the pixels that are set — the way a test states a shape by hand.</summary>
    /// <param name="width">The picture's width in pixels.</param>
    /// <param name="height">The picture's height in pixels.</param>
    /// <param name="opaquePixels">The opaque pixels, in the picture's own grid.</param>
    public static SpriteMask FromPixels(int width, int height, IEnumerable<(int X, int Y)> opaquePixels)
    {
        var opaque = new bool[width * height];
        foreach ((int x, int y) in opaquePixels)
        {
            opaque[(y * width) + x] = true;
        }

        return new SpriteMask(width, height, opaque);
    }

    /// <summary>
    /// True when the two pictures cover a common screen pixel. Each mask is placed by the rectangle its
    /// picture is DRAWN in — <see cref="Rendering.SpriteSet.DrawnRect"/> is the one definition of that
    /// placement, so the collision follows the art wherever the drawer puts it — and one picture pixel
    /// covers a square of screen pixels (the render scale).
    ///
    /// The cheap test comes first and the pixel walk only ever runs over the band the two pictures share.
    /// The DRAWN rectangles are the right thing to reject on, not the collision boxes: several pictures are
    /// drawn larger than the box that centres them (the spark is 8x7 art in a 4x4 box, the laser 6x6 in the
    /// same, the tank's birth frames bigger still), so a box-only rejection would drop the near misses the
    /// arcade counts.
    /// </summary>
    /// <param name="a">The first picture's mask.</param>
    /// <param name="aDrawnBounds">The rectangle <paramref name="a"/> is drawn in, in screen pixels.</param>
    /// <param name="b">The second picture's mask.</param>
    /// <param name="bDrawnBounds">The rectangle <paramref name="b"/> is drawn in, in screen pixels.</param>
    public static bool Overlap(SpriteMask a, Rectangle aDrawnBounds, SpriteMask b, Rectangle bDrawnBounds)
    {
        (int left, int top, int right, int bottom) = SharedScreenRect(aDrawnBounds, bDrawnBounds);
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

    /// <summary>The screen pixels both drawn rectangles cover, or an empty rectangle when they miss.</summary>
    private static (int Left, int Top, int Right, int Bottom) SharedScreenRect(Rectangle a, Rectangle b) =>
        (Math.Max(a.Left, b.Left), Math.Max(a.Top, b.Top), Math.Min(a.Right, b.Right), Math.Min(a.Bottom, b.Bottom));
}
