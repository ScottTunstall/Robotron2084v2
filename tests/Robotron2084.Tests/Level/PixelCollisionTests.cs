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
    private static readonly SpriteMask Dot = SpriteMask.CreateFromPixels(1, 1, [(0, 0)]);

    private static LevelParameters OneMikey()
    {
        return new LevelParameters(
            1,
            0,
            0,
            0,
            0,
            1,
            0);
    }

    private static PlayField CreateField(IPixelCollision? collision)
    {
        return new PlayFieldBuilder().WithParameters(OneMikey()).WithSeed(99).WithPixelCollision(collision).Build();
    }

    [Fact]
    public void ASpriteThatDoesNotTouchDoesNotRescue_EvenWhereTheBoxesOverlap()
    {
        var field = CreateField(new StubPixelCollision(false));
        var human = field.Entities.Family.Members[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Alive, human.LifeState);
        Assert.Equal(0, field.RescuesThisLife);
    }

    [Fact]
    public void ASpriteThatTouchesRescues_EvenWhereTheBoxesMiss()
    {
        var field = CreateField(new StubPixelCollision(true));
        field.SkipWaveStart();
        var human = field.Entities.Family.Members[0];

        // Well outside the player's box, so nothing but the sprite test could rescue this human.
        var inner = field.Wall.PlayfieldBounds;
        human.MoveTo(new IntVector2(inner.Center.X + ScreenSize.ToPortPixelsFromArcadePixels(60), inner.Center.Y));

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void AnEntityWithNoSpriteOfItsOwn_FallsBackToItsBox()
    {
        var field = CreateField(new StubPixelCollision(false, false));
        field.SkipWaveStart();
        var human = field.Entities.Family.Members[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void WithNoContactTestAtAll_TheBoxesDecide()
    {
        var field = CreateField(null);
        field.SkipWaveStart();
        var human = field.Entities.Family.Members[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    /// <summary>A contact test the test drives: every pair gets the same answer, whatever the boxes say.</summary>
    private sealed class StubPixelCollision(bool touching, bool hasShape = true) : IPixelCollision
    {
        public SpriteShape? GetShape(IEntity entity)
        {
            return hasShape ? new SpriteShape(Dot, new Rectangle(0, 0, 1, 1)) : null;
        }

        public bool Overlaps(SpriteShape a, SpriteShape b)
        {
            return touching;
        }
    }
}
