using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     The playfield's contact test (notes §118): with an <see cref="IPixelCollision" /> the sprites' pixels
///     decide, and without one — or for an entity that shows no sprite of its own — the collision boxes do.
/// </summary>
public sealed class PixelCollisionTests
{
    /// <summary>A one-pixel sprite, which is all the stub below needs to describe.</summary>
    private static readonly SpriteMask DotSpriteMask = SpriteMask.CreateFromPixels(1, 1, [(0, 0)]);

    private static WaveParameters OneMikey()
    {
        return new WaveParameters(
            1,
            0,
            0,
            0,
            0,
            1,
            0);
    }

    private static PlayField CreateField(IPixelCollision? pixelCollision)
    {
        return new PlayFieldBuilder().WithParameters(OneMikey()).WithSeed(99).WithPixelCollision(pixelCollision).Build();
    }

    [Fact]
    public void ASpriteThatDoesNotTouchDoesNotRescue_EvenWhereTheBoxesOverlap()
    {
        var playField = CreateField(new StubPixelCollision(false));
        var human = playField.FieldEntities.FamilyList.Members[0];
        human.MoveTo(playField.Player.Position);

        playField.Update(new GameTime());

        Assert.Equal(EntityLifeState.Alive, human.LifeState);
        Assert.Equal(0, playField.RescuesThisLife);
    }

    [Fact]
    public void ASpriteThatTouchesRescues_EvenWhereTheBoxesMiss()
    {
        var playField = CreateField(new StubPixelCollision(true));
        playField.SkipWaveStart();
        var human = playField.FieldEntities.FamilyList.Members[0];

        // Well outside the player's box, so nothing but the sprite test could rescue this human.
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        human.MoveTo(new IntVector2(inner.Center.X + ScreenSize.ToPortPixelsFromArcadePixels(60), inner.Center.Y));

        playField.Update(new GameTime());

        Assert.Equal(1, playField.RescuesThisLife);
    }

    [Fact]
    public void AnEntityWithNoSpriteOfItsOwn_FallsBackToItsBox()
    {
        var playField = CreateField(new StubPixelCollision(false, false));
        playField.SkipWaveStart();
        var human = playField.FieldEntities.FamilyList.Members[0];
        human.MoveTo(playField.Player.Position);

        playField.Update(new GameTime());

        Assert.Equal(1, playField.RescuesThisLife);
    }

    [Fact]
    public void WithNoContactTestAtAll_TheBoxesDecide()
    {
        var playField = CreateField(null);
        playField.SkipWaveStart();
        var human = playField.FieldEntities.FamilyList.Members[0];
        human.MoveTo(playField.Player.Position);

        playField.Update(new GameTime());

        Assert.Equal(1, playField.RescuesThisLife);
    }

    /// <summary>A contact test the test drives: every pair gets the same answer, whatever the boxes say.</summary>
    private sealed class StubPixelCollision(bool touching, bool hasShape = true) : IPixelCollision
    {
        public SpriteShape? GetShape(IEntity entity)
        {
            return hasShape ? new SpriteShape(DotSpriteMask, new Rectangle(0, 0, 1, 1)) : null;
        }

        public bool Overlaps(SpriteShape a, SpriteShape b)
        {
            return touching;
        }
    }
}
