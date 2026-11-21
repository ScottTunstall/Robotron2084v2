using Robotron2084.Core.Models;
using Robotron2084.Entities;
using Robotron2084.Entities.Collections;
using Xunit;

namespace Robotron2084.Tests.Entities.Collections;

/// <summary>The grunts on the field.</summary>
public sealed class GruntListTests
{
    [Fact]
    public void SpeedingUpTheAliveLeavesTheDeadAlone()
    {
        var alive = CreateGrunt(16);
        var dead = CreateGrunt(16);
        dead.Kill();
        GruntList grunts = [alive, dead];

        grunts.SpeedUpAlive(4);

        Assert.Equal(14, alive.MoveIntervalMaxBeats); // 16 x 7/8
        Assert.Equal(16, dead.MoveIntervalMaxBeats);
    }

    [Fact]
    public void TheFastestMoveIntervalIsTheShortestWaitOfAnyLivingGrunt()
    {
        var slow = CreateGrunt(20);
        var fast = CreateGrunt(9);
        GruntList grunts = [slow, fast];

        Assert.Equal(9, grunts.GetFastestMoveInterval(15));

        fast.Kill();
        Assert.Equal(20, grunts.GetFastestMoveInterval(15));

        slow.Kill();
        Assert.Equal(15, grunts.GetFastestMoveInterval(15));
    }

    private static Grunt CreateGrunt(int moveIntervalMaxBeats)
    {
        return new Grunt(TestEntityParams.Create(new Random(1)), new IntVector2(100, 100), moveIntervalMaxBeats);
    }
}
