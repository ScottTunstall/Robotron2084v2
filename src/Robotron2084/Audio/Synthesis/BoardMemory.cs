namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// The board's variables that outlast a sound: the random number generator, two scratch bytes some
/// sounds read without setting first, and the counters that make a repeated sound number change each
/// time it is sent.
/// </summary>
/// <remarks>
/// Original source: <c>VSNDRM3.SRC</c>, the <c>GLOBALS</c> and <c>TEMPORARIES</c> blocks (<c>SP1FLG</c>,
/// <c>B2FLG</c>, <c>HI</c>, <c>LO</c>, <c>TEMPA</c>, <c>TEMPB</c>). The board's RAM starts as zeros
/// except <c>HI</c>, which <c>SETUP</c> loads.
/// </remarks>
internal sealed class BoardMemory
{
    /// <summary>The value <c>SETUP</c> leaves in <c>HI</c>: the byte it last wrote to the output chip (<c>LDAA #$3C</c>).</summary>
    private const byte StartingRandomHigh = 0x3C;

    /// <summary>The bit that shifts in at the top of a byte when it rotates right. The new random bit is multiplied by it and the result is combined into <see cref="RandomHigh"/>.</summary>
    private const int TopBit = 0x80;

    /// <summary>How far the generator shifts a byte before mixing it in (the three <c>LSR</c>s).</summary>
    private const int MixShift = 3;

    private int _bitFromHigh;

    /// <summary>The random number generator's high byte (<c>HI</c>).</summary>
    public byte RandomHigh { get; private set; } = StartingRandomHigh;

    /// <summary>The random number generator's low byte (<c>LO</c>), which the noise sounds also use as a random level.</summary>
    public byte RandomLow { get; private set; }

    /// <summary>A scratch byte (<c>TEMPA</c>) that the radio sound reads without setting first.</summary>
    public byte ScratchA { get; set; }

    /// <summary>A scratch byte (<c>TEMPB</c>) that the scream sound counts up from without setting first.</summary>
    public byte ScratchB { get; set; }

    /// <summary>How many times in a row the spinner sound has been sent (<c>SP1FLG</c>); any other sound number clears it.</summary>
    public byte SpinnerSends { get; set; }

    /// <summary>True while the laser ball bonus sound is being sent over and over (<c>B2FLG</c>); any other sound number clears it.</summary>
    public bool IsLaserBallBonusRepeating { get; set; }

    /// <summary>True while the first background sound is on (<c>BG1FLG</c>): it plays again after every other sound until <c>BGEND</c>.</summary>
    public bool IsBackground1On { get; set; }

    /// <summary>Which of the second background sound's 29 pitches is on, or 0 for off (<c>BG2FLG</c>): it plays again after every other sound until <c>BGEND</c>.</summary>
    public byte Background2Level { get; set; }

    /// <summary>True after <c>ORGANT</c> (<c>ORGFLG</c>): the next sound number is an organ tune number instead of a sound.</summary>
    public bool IsOrganTuneNext { get; set; }

    /// <summary>The bit that last fell out of the bottom of <c>LO</c> (the carry flag after <c>ROR LO</c>).</summary>
    public bool RandomBitOut { get; private set; }

    /// <summary>
    /// Loads the random bytes directly: <c>STX HI</c> writes <c>HI</c> and <c>LO</c> together (the crowd
    /// roar seeds them), and the oscillator sound writes <c>LO</c> alone because <c>RANDOM EQU LO</c>.
    /// </summary>
    /// <param name="high">The new <c>HI</c>.</param>
    /// <param name="low">The new <c>LO</c>.</param>
    public void SetRandom(byte high, byte low)
    {
        RandomHigh = high;
        RandomLow = low;
    }

    /// <summary>
    /// The first half of a random step (<c>ROR HI</c>): a bit made from <paramref name="mixedWith"/> and
    /// <c>LO</c> shifts in at the top of <c>HI</c>.
    /// </summary>
    /// <param name="mixedWith">The byte mixed into the new bit: <c>LO</c> itself for most sounds, the output level for the filtered noise.</param>
    public void RotateRandomHigh(byte mixedWith)
    {
        int newBit = ((mixedWith >> MixShift) ^ RandomLow) & 1;
        _bitFromHigh = RandomHigh & 1;
        RandomHigh = (byte)((newBit * TopBit) | (RandomHigh >> 1));
    }

    /// <summary>
    /// The second half of a random step (<c>ROR LO</c>): the bit that fell out of <c>HI</c> shifts in at
    /// the top of <c>LO</c>, and the bit that falls out of <c>LO</c> becomes <see cref="RandomBitOut"/>.
    /// </summary>
    public void RotateRandomLow()
    {
        RandomBitOut = (RandomLow & 1) != 0;
        RandomLow = (byte)((_bitFromHigh * TopBit) | (RandomLow >> 1));
    }
}
