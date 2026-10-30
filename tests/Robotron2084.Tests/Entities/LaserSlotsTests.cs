using Robotron2084.Core;
using Robotron2084.Entities;
using Xunit;

namespace Robotron2084.Tests;

public sealed class LaserSlotsTests
{
    [Fact]
    public void Fire_AllowsThreeLiveLasersThenHasNoFreeSlot()
    {
        LaserSlots slots = new(TestSprites.Shared);

        slots.Fire(new IntVector2(100, 100), Direction8.Up);
        slots.Fire(new IntVector2(120, 100), Direction8.Up);
        slots.Fire(new IntVector2(140, 100), Direction8.Up);
        Assert.False(slots.HasFreeSlot());
        Assert.Equal(3, slots.GetActiveLasers().Count());
    }

    [Fact]
    public void Fire_AfterDeactivation_ReusesTheFreedSlot()
    {
        LaserSlots slots = new(TestSprites.Shared);

        for (var i = 0; i < LaserSlots.Capacity; i++)
            slots.Fire(new IntVector2(100 + i * 20, 100), Direction8.Up);

        slots.Slots[0]!.Kill();

        var laser = slots.FireAndGet(new IntVector2(300, 300), Direction8.Right);
        Assert.NotNull(laser);
        Assert.Equal(Direction8.Right, laser!.Direction);
        Assert.Equal(3, slots.GetActiveLasers().Count());
    }
}
