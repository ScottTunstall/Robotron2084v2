namespace Robotron2084.Entities.Collections;

/// <summary>The spheroids on the field.</summary>
public sealed class SpheroidList : EntityList<Spheroid>
{
    /// <summary>How many enforcers turn back into one spheroid when a life ends.</summary>
    private const int EnforcersPerSpheroid = 4;

    /// <summary>
    ///     Works out how many spheroids the next life starts with, once the enforcers left have turned back into them:
    ///     one for every four, and at least one when there are enforcers but no spheroids.
    /// </summary>
    /// <param name="enforcersLeft">How many enforcers are still alive.</param>
    /// <param name="startedWith">How many spheroids the life began with, which is the most there can be.</param>
    /// <returns>How many spheroids there are.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either number is below 0.</exception>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLEND</c> (notes §134).</remarks>
    public int CountForNextLife(int enforcersLeft, int startedWith)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(enforcersLeft);
        ArgumentOutOfRangeException.ThrowIfNegative(startedWith);

        var spheroids = CountAlive();
        if (enforcersLeft == 0) return spheroids;

        var spheroidsFromEnforcers = enforcersLeft / EnforcersPerSpheroid;
        if (spheroidsFromEnforcers == 0 && spheroids == 0) spheroidsFromEnforcers = 1;

        return Math.Min(spheroidsFromEnforcers + spheroids, startedWith);
    }
}
