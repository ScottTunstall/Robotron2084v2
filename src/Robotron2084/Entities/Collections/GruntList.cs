using Robotron2084.Entities.Extensions;

namespace Robotron2084.Entities.Collections;

/// <summary>The grunts on the field.</summary>
public sealed class GruntList : EntityList<Grunt>
{
    /// <summary>Gets the most beats any living grunt waits between steps, at the speed the grunts have reached.</summary>
    /// <param name="whenNoGrunts">The answer when no grunt is alive.</param>
    /// <returns>The smallest of the grunts' waits: the fastest grunt's.</returns>
    public int GetFastestMoveInterval(int whenNoGrunts)
    {
        return this.Where(grunt => grunt.IsAlive()).Select(grunt => grunt.MoveIntervalMaxBeats).DefaultIfEmpty(whenNoGrunts).Min();
    }

    /// <summary>Says whether any grunt took a step in the last tick.</summary>
    /// <returns>True when at least one grunt stepped.</returns>
    public bool HasAnyStepped()
    {
        return this.Any(grunt => grunt.SteppedThisUpdate);
    }

    /// <summary>Makes every living grunt a little faster, as happens each time a grunt dies.</summary>
    /// <param name="floorBeats">The fewest beats a grunt may be brought down to.</param>
    public void SpeedUpAlive(int floorBeats)
    {
        foreach (var grunt in this.Where(grunt => grunt.IsAlive())) grunt.SpeedUp(floorBeats);
    }

    /// <summary>Makes every living grunt faster as the wave goes on.</summary>
    /// <param name="floorBeats">The fewest beats a grunt may be brought down to.</param>
    /// <param name="stepBeats">How many beats each grunt's longest wait is cut by.</param>
    public void SpeedUpAliveAsWaveGoesOn(int floorBeats, int stepBeats)
    {
        foreach (var grunt in this.Where(grunt => grunt.IsAlive())) grunt.SpeedUpAsWaveGoesOn(floorBeats, stepBeats);
    }
}
