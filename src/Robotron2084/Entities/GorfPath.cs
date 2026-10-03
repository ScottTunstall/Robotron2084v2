namespace Robotron2084.Entities;

/// <summary>The arc of one of Gorf's hops, worked out with whole numbers and no trigonometry.</summary>
/// <remarks>
/// A hop is the arch of a parabola, which is close to the top of a sine wave: it leaves the ground, reaches its full height half way through, and lands where
/// it left, so a string of hops is a string of arches along the ground.
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
