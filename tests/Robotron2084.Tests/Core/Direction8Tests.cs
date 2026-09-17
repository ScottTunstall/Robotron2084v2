using Robotron2084.Core;
using Xunit;

namespace Robotron2084.Tests;

public sealed class Direction8Tests
{
    [Fact]
    public void Opposite_ReturnsAllFourOppositePairs()
    {
        Assert.Equal(Direction8.Down, Direction8.Up.Opposite());
        Assert.Equal(Direction8.Up, Direction8.Down.Opposite());
        Assert.Equal(Direction8.Left, Direction8.Right.Opposite());
        Assert.Equal(Direction8.Right, Direction8.Left.Opposite());
        Assert.Equal(Direction8.DownLeft, Direction8.UpRight.Opposite());
        Assert.Equal(Direction8.UpRight, Direction8.DownLeft.Opposite());
        Assert.Equal(Direction8.UpLeft, Direction8.DownRight.Opposite());
        Assert.Equal(Direction8.DownRight, Direction8.UpLeft.Opposite());
    }

    [Fact]
    public void FromDelta_Zero_ReturnsNull() => Assert.Null(Direction8Extensions.FromDelta(IntVector2.Zero));

    [Theory]
    [InlineData(5, 0, Direction8.Right)]
    [InlineData(0, 5, Direction8.Down)]
    [InlineData(0, -5, Direction8.Up)]
    [InlineData(-5, 0, Direction8.Left)]
    public void FromDelta_CardinalDelta_MapsToCardinalDirection(int x, int y, Direction8 expected)
    {
        Assert.Equal(expected, Direction8Extensions.FromDelta(new IntVector2(x, y)));
    }

    [Theory]
    [InlineData(3, 4, Direction8.DownRight)]
    [InlineData(-3, 4, Direction8.DownLeft)]
    [InlineData(-3, -4, Direction8.UpLeft)]
    [InlineData(3, -4, Direction8.UpRight)]
    public void FromDelta_DiagonalDelta_MapsToDiagonalDirection(int x, int y, Direction8 expected)
    {
        Assert.Equal(expected, Direction8Extensions.FromDelta(new IntVector2(x, y)));
    }

    [Theory]
    [InlineData(Direction8.Up, 0, -1)]
    [InlineData(Direction8.UpRight, 1, -1)]
    [InlineData(Direction8.Right, 1, 0)]
    [InlineData(Direction8.DownRight, 1, 1)]
    [InlineData(Direction8.Down, 0, 1)]
    [InlineData(Direction8.DownLeft, -1, 1)]
    [InlineData(Direction8.Left, -1, 0)]
    [InlineData(Direction8.UpLeft, -1, -1)]
    public void ToIntVector_ReturnsUnitSignVectorPerAxis(Direction8 direction, int expectedX, int expectedY)
    {
        IntVector2 vector = direction.ToIntVector();
        Assert.Equal(expectedX, vector.X);
        Assert.Equal(expectedY, vector.Y);
    }
}
