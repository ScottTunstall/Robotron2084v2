using Robotron2084.Core;
using Robotron2084.Entities;
using Xunit;

namespace Robotron2084.Tests;

public sealed class LaserSlotsTests
{
    [Fact]
    public void TryFire_AllowsThreeLiveLasersThenRefusesTheFourth()
    {
        LaserSlots slots = new();

        Assert.True(slots.TryFire(new IntVector2(100, 100), Direction8.Up, out _));
        Assert.True(slots.TryFire(new IntVector2(120, 100), Direction8.Up, out _));
        Assert.True(slots.TryFire(new IntVector2(140, 100), Direction8.Up, out _));
        Assert.False(slots.TryFire(new IntVector2(160, 100), Direction8.Up, out _));
        Assert.Equal(3, slots.ActiveLasers.Count());
    }

    [Fact]
    public void TryFire_AfterDeactivation_ReusesTheFreedSlot()
    {
        LaserSlots slots = new();

        for (int i = 0; i < LaserSlots.Capacity; i++)
        {
            Assert.True(slots.TryFire(new IntVector2(100 + i * 20, 100), Direction8.Up, out _));
        }

        slots.Slots[0]!.Deactivate();

        Assert.True(slots.TryFire(new IntVector2(300, 300), Direction8.Right, out PlayerLaser? laser));
        Assert.NotNull(laser);
        Assert.Equal(Direction8.Right, laser!.Direction);
        Assert.Equal(3, slots.ActiveLasers.Count());
    }
}
