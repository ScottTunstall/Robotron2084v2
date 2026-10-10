namespace Robotron2084.Entities;

/// <summary>Works out the curve Gorf follows through the air on each hop, using whole numbers only.</summary>
/// <remarks>
/// A hop is shaped like an arch: Gorf leaves the ground, is at full height half way through, and lands at the
/// height it left from. A string of hops makes a string of arches along the ground.
/// </remarks>
public static class GorfPath
{
    /// <summary>Works out how high above the ground Gorf is part of the way through a hop.</summary>
    /// <param name="stepInHop">How many steps of the hop have been taken, from 0 (leaving the ground) to <paramref name="hopSteps"/> (landed).</param>
    /// <param name="hopSteps">How many steps the whole hop takes. It must be at least 2.</param>
    /// <param name="height">The height of the top of the hop, in rows (or pixels: the answer is in the same unit).</param>
    /// <returns>The height above the ground, from 0 up to <paramref name="height"/> and back to 0.</returns>
    public static int GetHopHeight(int stepInHop, int hopSteps, int height) =>
        height * 4 * stepInHop * (hopSteps - stepInHop) / (hopSteps * hopSteps);
}
