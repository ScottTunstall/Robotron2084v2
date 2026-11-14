namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     How many times the sound board's count-down loops run. They take one away before testing for zero,
///     so a count of 0 wraps round and runs the most times the counter can hold.
/// </summary>
internal static class CountdownLoop
{
    /// <summary>The runs an 8-bit count of 0 gives.</summary>
    private const int ByteWrap = 256;

    /// <summary>The runs a 16-bit count of 0 gives.</summary>
    private const int WordWrap = 65_536;

    /// <summary>How many times a loop counting down an 8-bit register runs.</summary>
    /// <param name="count">The starting count.</param>
    /// <returns>The count, or 256 for 0.</returns>
    public static int Runs(byte count)
    {
        return count == 0 ? ByteWrap : count;
    }

    /// <summary>How many times a loop counting down the 16-bit index register runs.</summary>
    /// <param name="count">The starting count.</param>
    /// <returns>The count, or 65,536 for 0.</returns>
    public static int Runs(ushort count)
    {
        return count == 0 ? WordWrap : count;
    }
}
