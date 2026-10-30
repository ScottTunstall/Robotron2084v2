using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Tests;

/// <summary>Lets a test fire a laser and get hold of it, which <see cref="LaserSlots.Fire" /> does not return.</summary>
internal static class LaserSlotsTestExtensions
{
    /// <summary>Fires a laser, and gives back the one that was just made.</summary>
    /// <param name="slots">The slots to fire into. One of them must be free.</param>
    /// <param name="position">Where the laser appears.</param>
    /// <param name="direction">The way it flies.</param>
    /// <returns>The new laser.</returns>
    public static PlayerLaser FireAndGet(this LaserSlots slots, IntVector2 position, Direction8 direction)
    {
        var before = slots.GetActiveLasers().ToHashSet();
        slots.Fire(position, direction);
        return slots.GetActiveLasers().Single(laser => !before.Contains(laser));
    }
}
