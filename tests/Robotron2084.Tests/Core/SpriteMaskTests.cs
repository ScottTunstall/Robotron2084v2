using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Xunit;

namespace Robotron2084.Tests.Core;

/// <summary>
/// The opaque-pixel masks and the contact test they drive (notes §118): a picture's shape, and the
/// screen pixels two of them share wherever their boxes put them.
/// </summary>
public sealed class SpriteMaskTests
{
    [Fact]
    public void IsOpaque_IsFalseOutsideThePicture()
    {
        SpriteMask mask = SpriteMask.FromPixels(2, 3, [(1, 2)]);

        Assert.True(mask.IsOpaque(1, 2));
        Assert.False(mask.IsOpaque(0, 0));
        Assert.False(mask.IsOpaque(2, 2));   // one past the right edge
        Assert.False(mask.IsOpaque(-1, 0));
        Assert.False(mask.IsOpaque(0, 3));
    }

    [Fact]
    public void TwoPicturesTouchOnlyWhereBothHaveAPixel_NotWhereTheirBoxesOverlap()
    {
        // Two 4x4 pictures: one marked only at its top-left, the other only at its bottom-right.
        SpriteMask topLeft = SpriteMask.FromPixels(4, 4, [(0, 0)]);
        SpriteMask bottomRight = SpriteMask.FromPixels(4, 4, [(3, 3)]);
        var box = new Rectangle(100, 100, 4, 4);

        // Drawn in the same place the two marks are three pixels apart: the boxes overlap, the art does not.
        Assert.False(SpriteMask.Overlap(topLeft, box, bottomRight, box));

        // Slide the second picture up and left until its mark lands on the first one's.
        Assert.True(SpriteMask.Overlap(topLeft, box, bottomRight, new Rectangle(97, 97, 4, 4)));
    }

    [Fact]
    public void OnePicturePixelCoversARenderScaleBlockOfScreenPixels()
    {
        // A 2x2 picture drawn 4x4 screen pixels across: one picture pixel is a 2x2 screen block.
        SpriteMask topLeft = SpriteMask.FromPixels(2, 2, [(0, 0)]);
        SpriteMask bottomRight = SpriteMask.FromPixels(2, 2, [(1, 1)]);
        var a = new Rectangle(100, 100, 4, 4);

        // In the same box the two marks cannot reach each other (0..2 and 2..4 screen pixels)...
        Assert.False(SpriteMask.Overlap(topLeft, a, bottomRight, a));

        // ...but two screen pixels up-left the second mark covers screen pixel 100.
        Assert.True(SpriteMask.Overlap(topLeft, a, bottomRight, new Rectangle(98, 98, 4, 4)));
        Assert.False(SpriteMask.Overlap(topLeft, a, bottomRight, new Rectangle(96, 96, 4, 4)));
    }

    [Fact]
    public void BoxesThatMissNeverTouch_WhateverThePixels()
    {
        SpriteMask solid = SpriteMask.FromPixels(2, 2, [(0, 0), (1, 0), (0, 1), (1, 1)]);
        var box = new Rectangle(0, 0, 2, 2);

        Assert.True(SpriteMask.Overlap(solid, box, solid, box));

        // Adjacent and apart, with no shared screen pixel.
        Assert.False(SpriteMask.Overlap(solid, box, solid, new Rectangle(2, 0, 2, 2)));
        Assert.False(SpriteMask.Overlap(solid, box, solid, new Rectangle(0, 2, 2, 2)));
    }
}
