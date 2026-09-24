using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The playfield's contact test (notes §118): with an <see cref="IPixelCollision"/> the pictures' pixels
/// decide, and without one — or for an entity that shows no picture of its own — the collision boxes do.
/// </summary>
public sealed class PixelCollisionTests
{
    /// <summary>A one-pixel picture, which is all the stub below needs to describe.</summary>
    private static readonly SpriteMask Dot = SpriteMask.FromPixels(1, 1, [(0, 0)]);

    /// <summary>A contact test the test drives: every pair gets the same answer, whatever the boxes say.</summary>
    private sealed class StubPixelCollision(bool touching, bool hasShape = true) : IPixelCollision
    {
        public PictureShape? ShapeOf(IEntity entity) =>
            hasShape ? new PictureShape(Dot, new Rectangle(0, 0, 1, 1)) : null;

        public bool Overlaps(PictureShape a, PictureShape b) => touching;
    }

    private static LevelParameters OneMikey => new(
        LevelNumber: 1,
        GruntCount: 0,
        ElectrodeCount: 0,
        MomCount: 0,
        DadCount: 0,
        MikeyCount: 1,
        HulkCount: 0);

    private static PlayField CreateField(IPixelCollision? collision) =>
        new(TestSprites.Shared, 
            OneMikey,
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(99),
            startingLives: 3,
            pixelCollision: collision);

    [Fact]
    public void APictureThatDoesNotTouchDoesNotRescue_EvenWhereTheBoxesOverlap()
    {
        PlayField field = CreateField(new StubPixelCollision(touching: false));
        Human human = field.Humans[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Alive, human.LifeState);
        Assert.Equal(0, field.RescuesThisLife);
    }

    [Fact]
    public void APictureThatTouchesRescues_EvenWhereTheBoxesMiss()
    {
        PlayField field = CreateField(new StubPixelCollision(touching: true));
        Human human = field.Humans[0];

        // Well outside the player's box, so nothing but the picture test could rescue this human.
        Rectangle inner = field.Wall.PlayfieldBounds;
        human.MoveTo(new IntVector2(inner.Center.X + ScreenSize.Scaled(60), inner.Center.Y));

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void AnEntityWithNoPictureOfItsOwn_FallsBackToItsBox()
    {
        PlayField field = CreateField(new StubPixelCollision(touching: false, hasShape: false));
        Human human = field.Humans[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void WithNoContactTestAtAll_TheBoxesDecide()
    {
        PlayField field = CreateField(null);
        Human human = field.Humans[0];
        human.MoveTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(1, field.RescuesThisLife);
    }
}