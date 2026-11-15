using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;
using Robotron2084.Core;
using Robotron2084.Entities.Models;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>What a tank shell is aimed with.</summary>
public sealed class TankShellAimTests
{
    private static readonly Rectangle Bounds = new(20, 30, 600, 400);

    [Fact]
    public void KeepsWhatItIsGiven()
    {
        var aim = new TankShellAim(new IntVector2(5, 6), 3, Bounds);

        Assert.Equal(new IntVector2(5, 6), aim.PlayerPosition);
        Assert.Equal(3, aim.ShellSpeed);
        Assert.Equal(Bounds, aim.PlayfieldBounds);
    }

    [Fact]
    public void RefusesAShellSpeedBelowZero()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new TankShellAim(IntVector2.Zero, -1, Bounds));
    }

    [Fact]
    public void RefusesPlayfieldBoundsWithNoArea()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TankShellAim(IntVector2.Zero, 3, new Rectangle(0, 0, 0, 0)));

        Assert.Equal("playfieldBounds", exception.ParamName);
    }
}
