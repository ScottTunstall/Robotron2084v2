using System;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// (notes §94) The nearest-living-robot accessor the attract demo's
/// phony player steers by. Manhattan distance, same rule as the ROM's GETHTG
/// (notes §90/§94.3); every robot kind counts; the dead are ignored.
/// </summary>
public sealed class PlayFieldNearestRobotTests
{
    private static readonly Microsoft.Xna.Framework.Rectangle Bounds =
        new(ScreenSize.ToPortPixelsFromArcadePixels(20), ScreenSize.ToPortPixelsFromArcadePixels(20), ScreenSize.Width - ScreenSize.ToPortPixelsFromArcadePixels(40), ScreenSize.Height - ScreenSize.ToPortPixelsFromArcadePixels(40));

    private static PlayField EmptyField()
    {
        var parameters = new LevelParameters(LevelNumber: 1);
        return new PlayFieldBuilder().WithParameters(parameters).WithBounds(Bounds).WithSeed(42).Build();
    }

    [Fact]
    public void NearestLivingRobotPositionTo_WithNoRobots_IsNull()
    {
        PlayField field = EmptyField();

        Assert.Null(field.Entities.GetNearestLivingRobotPosition(field.Player.Position));
    }

    [Fact]
    public void NearestLivingRobotPositionTo_ReturnsTheCloserOfTwo()
    {
        PlayField field = EmptyField();
        IntVector2 player = field.Player.Position;
        IntVector2 far = player + new IntVector2(0, 300);
        IntVector2 near = player + new IntVector2(0, 100);

        field.SpawnEnforcer(far);
        Tank nearTank = field.SpawnTank(near);

        Assert.Equal(nearTank.Position, field.Entities.GetNearestLivingRobotPosition(player));
    }

    [Fact]
    public void NearestLivingRobotPositionTo_IgnoresDeadRobots()
    {
        PlayField field = EmptyField();
        IntVector2 player = field.Player.Position;
        IntVector2 far = player + new IntVector2(0, 300);
        IntVector2 near = player + new IntVector2(0, 100);

        field.SpawnEnforcer(far);
        Tank tank = field.SpawnTank(near);
        tank.Kill();

        Assert.Equal(far, field.Entities.GetNearestLivingRobotPosition(player));
    }
}
