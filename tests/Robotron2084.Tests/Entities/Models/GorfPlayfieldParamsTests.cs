using Microsoft.Xna.Framework;
using Robotron2084.Entities.Models;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>The playfield a Gorf is built for.</summary>
public sealed class GorfPlayfieldTests
{
    private static readonly Rectangle Bounds = new(20, 30, 600, 400);

    [Fact]
    public void KeepsTheBoundsAndTheBoxToAvoid()
    {
        var box = new Rectangle(100, 100, 20, 30);

        var playfield = new GorfPlayfield(Bounds, box);

        Assert.Equal(Bounds, playfield.Bounds);
        Assert.Equal(box, playfield.PlayerBoxToAvoid);
    }

    [Fact]
    public void HasNoBoxToAvoidUnlessOneIsGiven()
    {
        Assert.Null(new GorfPlayfield(Bounds).PlayerBoxToAvoid);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void RefusesBoundsWithNoArea(int width, int height)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new GorfPlayfield(new Rectangle(0, 0, width, height)));
    }

    [Fact]
    public void RefusesABoxToAvoidWithNoArea()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GorfPlayfield(Bounds, new Rectangle(5, 5, 0, 10)));

        Assert.Equal("playerBoxToAvoid", exception.ParamName);
    }
}
