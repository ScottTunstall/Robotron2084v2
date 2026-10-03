using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The rules each entity keeps for itself, which used to sit in the playfield: whether a human can be got hold of,
/// where a tank or a quark may stand, how a list counts its living, and how the strip clip is made.
/// </summary>
public sealed class EntityOwnRulesTests
{
    [Fact]
    public void AHumanIsGraspable_OnlyWhileAliveAndNotInABrainsHold()
    {
        var human = new Human(TestSprites.Shared, new IntVector2(100, 100), HumanKind.Mommy, new Random(1));
        Assert.True(human.IsGraspable());

        human.BeginReprogramming();
        Assert.False(human.IsGraspable());

        var killed = new Human(TestSprites.Shared, new IntVector2(100, 100), HumanKind.Daddy, new Random(2));
        killed.Kill();
        Assert.False(killed.IsGraspable());
    }

    [Fact]
    public void ATanksSpot_IsMovedSoItsWholeBoxIsInsideThePlayfield()
    {
        var bounds = new Rectangle(20, 30, 400, 300);
        int width = ScreenSize.ToPortPixels(CollisionSizes.TankCollisionSize.Width);
        int height = ScreenSize.ToPortPixels(CollisionSizes.TankCollisionSize.Height);

        Assert.Equal(new IntVector2(20, 30), Tank.GetPositionInside(bounds, new IntVector2(0, 0)));
        Assert.Equal(new IntVector2(bounds.Right - width, bounds.Bottom - height), Tank.GetPositionInside(bounds, new IntVector2(900, 900)));
        Assert.Equal(new IntVector2(100, 100), Tank.GetPositionInside(bounds, new IntVector2(100, 100)));
    }

    [Fact]
    public void AQuarkStartsOnTheTopOrBottomWall_AndInsideTheSideWalls()
    {
        var bounds = new Rectangle(20, 30, 400, 300);
        int width = ScreenSize.ToPortPixels(CollisionSizes.QuarkCollisionSize.Width);
        int height = ScreenSize.ToPortPixels(CollisionSizes.QuarkCollisionSize.Height);
        var random = new Random(5);
        bool sawTop = false;
        bool sawBottom = false;

        for (int i = 0; i < 100; i++)
        {
            IntVector2 start = Quark.GetStartPosition(bounds, random);
            Assert.InRange(start.X, bounds.X, bounds.Right - width);
            Assert.True(start.Y == bounds.Y || start.Y == bounds.Bottom - height, $"a quark started at row {start.Y}");
            sawTop |= start.Y == bounds.Y;
            sawBottom |= start.Y == bounds.Bottom - height;
        }

        Assert.True(sawTop && sawBottom);
    }

    [Fact]
    public void AnEntityListCountsItsLiving_UntilTheDeadArePruned()
    {
        var list = new EntityList<Electrode>
        {
            new Electrode(TestSprites.Shared, new IntVector2(10, 10)),
            new Electrode(TestSprites.Shared, new IntVector2(50, 10)),
        };
        Assert.Equal(2, list.GetLiveCount());

        // A shrivelling electrode is dying, not dead, so it still counts.
        list[0].Kill();
        Assert.Equal(2, list.GetLiveCount());
    }

    [Fact]
    public void TheStripClip_IsThePlayfieldInArcadePixels()
    {
        var bounds = new Rectangle(20, 30, 400, 300);

        StripClip clip = StripClip.CreateFromPortPixels(bounds);

        Assert.Equal(new StripClip(20 / ScreenSize.SpecScale, 420 / ScreenSize.SpecScale, 30 / ScreenSize.SpecScale, 330 / ScreenSize.SpecScale), clip);
    }

    [Fact]
    public void TheDemosPlayerChasesEveryKind_ExceptTheElectrodesAndTheShotsItDodges()
    {
        RobotKind[] notChased = RobotKinds.All.Where(kind => !kind.IsChasedByDemoPlayer).Select(kind => kind.Kind).ToArray();

        Assert.Equal([RobotKind.Electrode, RobotKind.Spark, RobotKind.TankShell], notChased);
    }

    [Fact]
    public void DistancesBetweenTwoPoints_InPixelsAndInColumnsAndRows()
    {
        var from = new IntVector2(10, 10);
        var to = new IntVector2(10 + ScreenSize.ToPortPixelsFromColumns(3), 10 - ScreenSize.ToPortPixels(5));

        Assert.Equal(ScreenSize.ToPortPixelsFromColumns(3) + ScreenSize.ToPortPixels(5), from.GetManhattanDistance(to));
        Assert.Equal(3 + 5, ScreenSize.ToColumnAndRowDistance(from, to));
    }
}
