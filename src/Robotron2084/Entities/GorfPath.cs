namespace Robotron2084.Entities;

/// <summary>The up-and-down wave Gorf flies along, worked out with whole numbers and no trigonometry.</summary>
/// <remarks>
/// Each half of the wave is the arch of a parabola, which is close to the arch of a sine wave: it leaves the middle line, climbs to the
/// full height a quarter of the way through, and comes back down to the line at the half, then does the same on the other side.
/// </remarks>
public static class GorfPath
{
    /// <summary>Works out how far above or below its flight line Gorf is.</summary>
    /// <param name="step">How many steps Gorf has taken.</param>
    /// <param name="period">How many steps make one whole wave. It must be at least 4.</param>
    /// <param name="amplitude">The height of the wave, in rows (or pixels: the answer is in the same unit).</param>
    /// <returns>The distance from the flight line: positive on the first half of the wave, negative on the second.</returns>
    public static int GetOffset(int step, int period, int amplitude)
    {
        int half = period / 2;
        int position = step % period;
        int along = position % half;
        int offset = amplitude * 4 * along * (half - along) / (half * half);
        return position < half ? offset : -offset;
    }
}
