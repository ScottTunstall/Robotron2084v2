namespace Robotron2084.Entities.Models;

/// <summary>How many grunts a Gorf drops, and how its drops are spaced along its way across.</summary>
public sealed record GorfDropParams
{
    /// <summary>Makes the plan for a Gorf's drops.</summary>
    /// <param name="maxDropsX2">
    ///     This wave's limit on how many grunts it drops at once. The number is picked the way a spheroid
    ///     picks its enforcers.
    /// </param>
    /// <param name="gruntDropCount">
    ///     How many grunts it drops in all, one at a time, evenly spaced along its way across, so they come gradually. It
    ///     hands any it has not dropped yet to a Gorf that comes back if it is killed first. 0 leaves its three stops to
    ///     chance (<paramref name="maxDropsX2" />).
    /// </param>
    /// <param name="dropIntervalSteps">
    ///     How many steps pass between one drop and the next, or 0 to spread the drops evenly over its way across. A Gorf
    ///     that comes back is given a shorter one than the Gorf it follows.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Any of them is below 0.</exception>
    public GorfDropParams(int maxDropsX2, int gruntDropCount = 0, int dropIntervalSteps = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDropsX2);
        ArgumentOutOfRangeException.ThrowIfNegative(gruntDropCount);
        ArgumentOutOfRangeException.ThrowIfNegative(dropIntervalSteps);

        MaxDropsX2 = maxDropsX2;
        GruntDropCount = gruntDropCount;
        DropIntervalSteps = dropIntervalSteps;
    }

    /// <summary>This wave's limit on how many grunts it drops at once.</summary>
    public int MaxDropsX2 { get; }

    /// <summary>How many grunts it drops in all, or 0 to leave its three stops to chance.</summary>
    public int GruntDropCount { get; }

    /// <summary>How many steps pass between one drop and the next, or 0 to spread the drops evenly.</summary>
    public int DropIntervalSteps { get; }
}
