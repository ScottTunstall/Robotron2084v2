namespace Robotron2084.Core;

/// <summary>The arcade's way of picking a number up to a limit, which is not an even spread.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRF.ASM</c>, the routine that gets a random number lower than or equal to a limit.</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>$D6B6</c>. It takes a random byte, and halves it until it is no
///             bigger than the
///             limit. A result of 0 becomes 1.
///         </item>
///     </list>
///     Halving makes the small numbers come up more often than the large ones, so this is not the same as picking evenly
///     from 1 to the limit. Enforcers' fire delays use it (notes §145).
/// </remarks>
public static class ArcadeRandom
{
    /// <summary>The sides of the random byte the arcade starts from.</summary>
    private const int ByteSides = 256;

    /// <summary>Picks a number from 1 up to <paramref name="limit" />, the way the arcade does.</summary>
    /// <param name="random">Where the random byte comes from.</param>
    /// <param name="limit">The biggest number that can be picked.</param>
    /// <returns>A number from 1 up to the limit.</returns>
    public static int PickUpTo(Random random, int limit)
    {
        var value = random.Next(ByteSides);
        while (value > limit) value >>= 1;

        return value == 0 ? 1 : value;
    }
}
