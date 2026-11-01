using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     (notes §94) The nearest-living-robot accessor the attract demo's
///     phony player steers by. Manhattan distance, same rule as the ROM's GETHTG
///     (notes §90/§94.3); every robot kind counts; the dead are ignored.
/// </summary>
public sealed class PlayFieldNearestRobotTests
{
    private static readonly Rectangle Bounds =
        new(ScreenSize.ToPortPixelsFromArcadePixels(20), ScreenSize.ToPortPixelsFromArcadePixels(20),
            ScreenSize.Width - ScreenSize.ToPortPixelsFromArcadePixels(40),
            ScreenSize.Height - ScreenSize.ToPortPixelsFromArcadePixels(40));

    private static PlayField EmptyField()
    {
        var waveParameters = new WaveParameters(1);
        return new PlayFieldBuilder().WithParameters(waveParameters).WithBounds(Bounds).WithSeed(42).Build();
    }

    [Fact]
    public void NearestLivingRobotPositionTo_WithNoRobots_IsNull()
    {
        var playField = EmptyField();

        Assert.Null(playField.FieldEntities.GetNearestLivingRobotPosition(playField.Player.Position));
    }

    [Fact]
    public void NearestLivingRobotPositionTo_ReturnsTheCloserOfTwo()
    {
        var playField = EmptyField();
        var player = playField.Player.Position;
        var far = player + new IntVector2(0, 300);
        var near = player + new IntVector2(0, 100);

        playField.SpawnEnforcer(far);
        var nearTank = playField.SpawnTank(near);

        Assert.Equal(nearTank.Position, playField.FieldEntities.GetNearestLivingRobotPosition(player));
    }

    [Fact]
    public void NearestLivingRobotPositionTo_IgnoresDeadRobots()
    {
        var playField = EmptyField();
        var player = playField.Player.Position;
        var far = player + new IntVector2(0, 300);
        var near = player + new IntVector2(0, 100);

        playField.SpawnEnforcer(far);
        var tank = playField.SpawnTank(near);
        tank.Kill();

        Assert.Equal(far, playField.FieldEntities.GetNearestLivingRobotPosition(player));
    }
}
