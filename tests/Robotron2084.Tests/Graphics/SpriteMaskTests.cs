using Microsoft.Xna.Framework;
using Robotron2084.Graphics;
using Xunit;

namespace Robotron2084.Tests.Core;

/// <summary>
///     The opaque-pixel masks and the contact test they drive (notes §118): a sprite's shape, and the
///     screen pixels two of them share wherever their boxes put them.
/// </summary>
public sealed class SpriteMaskTests
{
    [Fact]
    public void IsOpaque_IsFalseOutsideTheSprite()
    {
        var mask = SpriteMask.CreateFromPixels(2, 3, [(1, 2)]);

        Assert.True(mask.IsOpaque(1, 2));
        Assert.False(mask.IsOpaque(0, 0));
        Assert.False(mask.IsOpaque(2, 2)); // one past the right edge
        Assert.False(mask.IsOpaque(-1, 0));
        Assert.False(mask.IsOpaque(0, 3));
    }

    [Fact]
    public void TwoSpritesTouchOnlyWhereBothHaveAPixel_NotWhereTheirBoxesOverlap()
    {
        // Two 4x4 sprites: one marked only at its top-left, the other only at its bottom-right.
        var topLeft = SpriteMask.CreateFromPixels(4, 4, [(0, 0)]);
        var bottomRight = SpriteMask.CreateFromPixels(4, 4, [(3, 3)]);
        var box = new Rectangle(100, 100, 4, 4);

        // Drawn in the same place the two marks are three pixels apart: the boxes overlap, the sprites do not.
        Assert.False(SpriteMask.Overlaps(topLeft, box, bottomRight, box));

        // Slide the second sprite up and left until its mark lands on the first one's.
        Assert.True(SpriteMask.Overlaps(topLeft, box, bottomRight, new Rectangle(97, 97, 4, 4)));
    }

    [Fact]
    public void OneSpritePixelCoversARenderScaleBlockOfScreenPixels()
    {
        // A 2x2 sprite drawn 4x4 screen pixels across: one sprite pixel is a 2x2 screen block.
        var topLeft = SpriteMask.CreateFromPixels(2, 2, [(0, 0)]);
        var bottomRight = SpriteMask.CreateFromPixels(2, 2, [(1, 1)]);
        var a = new Rectangle(100, 100, 4, 4);

        // In the same box the two marks cannot reach each other (0..2 and 2..4 screen pixels)...
        Assert.False(SpriteMask.Overlaps(topLeft, a, bottomRight, a));

        // ...but two screen pixels up-left the second mark covers screen pixel 100.
        Assert.True(SpriteMask.Overlaps(topLeft, a, bottomRight, new Rectangle(98, 98, 4, 4)));
        Assert.False(SpriteMask.Overlaps(topLeft, a, bottomRight, new Rectangle(96, 96, 4, 4)));
    }

    [Fact]
    public void BoxesThatMissNeverTouch_WhateverThePixels()
    {
        var solid = SpriteMask.CreateFromPixels(2, 2, [(0, 0), (1, 0), (0, 1), (1, 1)]);
        var box = new Rectangle(0, 0, 2, 2);

        Assert.True(SpriteMask.Overlaps(solid, box, solid, box));

        // Adjacent and apart, with no shared screen pixel.
        Assert.False(SpriteMask.Overlaps(solid, box, solid, new Rectangle(2, 0, 2, 2)));
        Assert.False(SpriteMask.Overlaps(solid, box, solid, new Rectangle(0, 2, 2, 2)));
    }
}
