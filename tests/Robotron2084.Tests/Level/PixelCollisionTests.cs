using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The playfield's contact test (notes §118): with an <see cref="IPixelCollision"/> the sprites' pixels
/// decide, and without one — or for an entity that shows no sprite of its own — the collision boxes do.
/// </summary>
public sealed class PixelCollisionTests
{
    /// <summary>A one-pixel sprite, which is all the stub below needs to describe.</summary>
    private static readonly SpriteMask Dot = SpriteMask.CreateFromPixels(1, 1, [(0, 0)]);

    /// <summary>A contact test the test drives: every pair gets the same answer, whatever the boxes say.</summary>
    private sealed class StubPixelCollision(bool touching, bool hasShape = true) : IPixelCollision
    {
        public SpriteShape? GetShape(IEntity entity) =>
            hasShape ? new SpriteShape(Dot, new Rectangle(0, 0, 1, 1)) : null;

        public bool Overlaps(SpriteShape a, SpriteShape b) => touching;
    }

    private static LevelParameters OneMikey() => new(
        LevelNumber: 1,
        GruntCount: 0,
        ElectrodeCount: 0,
        MommyCount: 0,
        DaddyCount: 0,
        MikeyCount: 1,
        HulkCount: 0);

    private static PlayField CreateField(IPixelCollision? collision) =>
        new PlayFieldBuilder().WithParameters(OneMikey()).WithSeed(99).WithPixelCollision(collision).Build();

    [Fact]
    public void ASpriteThatDoesNotTouchDoesNotRescue_EvenWhereTheBoxesOverlap()
    {
        PlayField field = CreateField(new StubPixelCollision(touching: false));
        Human human = field.Entities.Family.Members[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Alive, human.LifeState);
        Assert.Equal(0, field.RescuesThisLife);
    }

    [Fact]
    public void ASpriteThatTouchesRescues_EvenWhereTheBoxesMiss()
    {
        PlayField field = CreateField(new StubPixelCollision(touching: true));
        field.SkipWaveStart();
        Human human = field.Entities.Family.Members[0];

        // Well outside the player's box, so nothing but the sprite test could rescue this human.
        Rectangle inner = field.Wall.PlayfieldBounds;
        human.MoveTo(new IntVector2(inner.Center.X + ScreenSize.ToPortPixelsFromArcadePixels(60), inner.Center.Y));

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void AnEntityWithNoSpriteOfItsOwn_FallsBackToItsBox()
    {
        PlayField field = CreateField(new StubPixelCollision(touching: false, hasShape: false));
        field.SkipWaveStart();
        Human human = field.Entities.Family.Members[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void WithNoContactTestAtAll_TheBoxesDecide()
    {
        PlayField field = CreateField(null);
        field.SkipWaveStart();
        Human human = field.Entities.Family.Members[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }
}