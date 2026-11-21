using Robotron2084.Core.Models;
using Robotron2084.Entities;
using Robotron2084.Entities.Collections;
using Xunit;

namespace Robotron2084.Tests.Entities.Collections;

/// <summary>The spheroids on the field (ROM <c>PLEND</c>, notes §134).</summary>
public sealed class SpheroidListTests
{
    [Theory]
    [InlineData(2, 0, 5, 2)] // no enforcers: the spheroids left
    [InlineData(1, 8, 5, 3)] // eight enforcers make two more
    [InlineData(0, 3, 5, 1)] // fewer than four, and no spheroid: still one
    [InlineData(1, 3, 5, 1)] // fewer than four, with a spheroid: none more
    [InlineData(4, 8, 5, 5)] // never more than the life began with
    public void EnforcersTurnBackIntoSpheroids_OneForEveryFour(int spheroids, int enforcersLeft, int startedWith, int expected)
    {
        Assert.Equal(expected, CreateSpheroids(spheroids).CountForNextLife(enforcersLeft, startedWith));
    }

    private static SpheroidList CreateSpheroids(int count)
    {
        var spheroids = new SpheroidList();
        for (var i = 0; i < count; i++)
            spheroids.Add(new Spheroid(TestEntityParams.Create(new Random(i)), new IntVector2(100 + i * 30, 100), 10, 24));
        return spheroids;
    }
}
