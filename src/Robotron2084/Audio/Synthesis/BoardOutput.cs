namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The board's output port as a sound routine sees it: the level last written to it, and the clock
///     cycles spent since then. Writing the port turns those cycles into one <see cref="OutputChange" />.
/// </summary>
internal sealed class BoardOutput
{
    private int _cyclesSinceLastChange;

    /// <summary>The level the routine last wrote to the port (what <c>LDAA SOUND</c> reads).</summary>
    public byte Level { get; private set; }

    /// <summary>Counts clock cycles spent on work that does not touch the port.</summary>
    /// <param name="cycles">The cycles spent.</param>
    public void Wait(int cycles)
    {
        _cyclesSinceLastChange += cycles;
    }

    /// <summary>Writes a level to the port (<c>STAA SOUND</c>).</summary>
    /// <param name="level">The level.</param>
    /// <returns>The change, timed from the last one.</returns>
    public OutputChange Store(byte level)
    {
        Wait(InstructionCycles.StoreExtended);
        return Change(level, InstructionCycles.StoreExtended);
    }

    /// <summary>Flips every bit of the port's level (<c>COM SOUND</c>), so a low level goes high and a high one low.</summary>
    /// <returns>The change, timed from the last one.</returns>
    public OutputChange Complement()
    {
        Wait(InstructionCycles.ModifyExtended);
        return Change((byte)~Level, InstructionCycles.ModifyExtended);
    }

    /// <summary>
    ///     Lets the cycles counted so far pass without changing the level, so that what the routine does next
    ///     happens at the right moment: if a new sound number cuts in first, it never happens at all.
    /// </summary>
    /// <returns>A change to the same level, timed from the last change.</returns>
    public OutputChange Pass()
    {
        return Change(Level, 0);
    }

    /// <summary>
    ///     Forgets the cycles counted so far and takes up the level the port really holds: what an interrupt leaves
    ///     behind.
    /// </summary>
    /// <param name="level">The level the port holds.</param>
    public void Restart(byte level)
    {
        Level = level;
        _cyclesSinceLastChange = 0;
    }

    /// <summary>Records a new level and starts counting again.</summary>
    /// <param name="level">The new level.</param>
    /// <param name="writeCycles">How long the instruction that writes it takes, or 0 for no write.</param>
    /// <returns>The change.</returns>
    private OutputChange Change(byte level, int writeCycles)
    {
        Level = level;
        var change = new OutputChange(_cyclesSinceLastChange, level, writeCycles);
        _cyclesSinceLastChange = 0;
        return change;
    }
}
