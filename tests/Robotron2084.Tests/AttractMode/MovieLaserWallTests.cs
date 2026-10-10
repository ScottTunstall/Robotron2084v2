using Robotron2084.AttractMode;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The story movie's laser bolts stop at the wall, as the ROM's object mover makes them (RRS22 <c>OPRC80</c>).</summary>
public sealed class MovieLaserWallTests
{
    [Theory]
    [InlineData(0x80, 0x0280)] // fired right, near the right wall
    [InlineData(0x0A, -0x0280)] // fired left, near the left wall
    public void ABoltNeverLeavesThePlayfield(int startColumn, int velocity)
    {
        var bolt = new MovieObject(null, 0, startColumn << 8, 100 << 8)
            { IsLaser = true, XVelocitySubpixels = velocity };

        for (var frame = 0; frame < 100; frame++) AttractObjectMachine.MoveLaserWithinTheWalls(bolt);

        Assert.InRange(bolt.GetColumn(), 7, 0x8F + 1 - 3);
    }
}
