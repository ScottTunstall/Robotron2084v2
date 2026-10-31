using Robotron2084.Core;
using Robotron2084.Entities;
using Xunit;

namespace Robotron2084.Tests;

public sealed class LaserSlotsTests
{
    [Fact]
    public void Fire_AllowsThreeLiveLasersThenHasNoFreeSlot()
    {
        LaserSlots laserSlots = new(TestSprites.SharedSpriteSet);

        laserSlots.Fire(new IntVector2(100, 100), Direction8.Up);
        laserSlots.Fire(new IntVector2(120, 100), Direction8.Up);
        laserSlots.Fire(new IntVector2(140, 100), Direction8.Up);
        Assert.False(laserSlots.HasFreeSlot());
        Assert.Equal(3, laserSlots.GetActiveLasers().Count());
    }

    [Fact]
    public void Fire_AfterDeactivation_ReusesTheFreedSlot()
    {
        LaserSlots laserSlots = new(TestSprites.SharedSpriteSet);

        for (var i = 0; i < LaserSlots.Capacity; i++)
            laserSlots.Fire(new IntVector2(100 + i * 20, 100), Direction8.Up);

        laserSlots.Slots[0]!.Kill();

        var laser = laserSlots.FireAndGet(new IntVector2(300, 300), Direction8.Right);
        Assert.NotNull(laser);
        Assert.Equal(Direction8.Right, laser!.Direction);
        Assert.Equal(3, laserSlots.GetActiveLasers().Count());
    }
}
