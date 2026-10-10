using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     An air raid siren: a triangle wave whose length is a count that climbs, then falls, by a sweep that
///     itself grows.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c>, routines <c>ZIREN</c> ("SIREN   AIR RAID"), <c>ZIRLOP</c>
///             ("FLAT TRIANGLE LOOP") and <c>ZIRT</c>.
///         </item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
///     The low byte of the length count (<c>TOP+1</c>) is not set before the sound, so it starts as whatever the
///     board's memory held; the port starts it at 0, as the board's memory is when the board is first switched on.
/// </remarks>
internal sealed class SirenSound
{
    /// <summary><c>ZIREN</c>'s first length (<c>LDAA #$FF</c>), whose high byte is the delay count.</summary>
    private const byte FirstLengthHigh = 0xFF;

    /// <summary><c>ZIREN</c>'s first sweep (<c>LDX #$FEC0</c>): negative, so the count falls.</summary>
    private const ushort FirstSweep = 0xFEC0;

    private const byte FirstSlope = 0x20;

    private const ushort FirstEnd = 0xFFE0;

    /// <summary>The second pass's slope and end (<c>LDAA #$1</c>, <c>LDX #$44</c>).</summary>
    private const byte SecondSlope = 0x01;

    private const ushort SecondEnd = 0x0044;

    /// <summary>The rounds of the triangle played at each sweep (<c>LDX #$10</c>).</summary>
    private const int RoundsPerSweep = 0x10;

    /// <summary>The step each level of the triangle moves by (<c>ADDA #$20</c>, <c>SUBA #$20</c>).</summary>
    private const int TriangleStep = 0x20;

    /// <summary>The wait unit inside <c>ZIRT</c>: <c>LDAA #$2</c>, then <c>DECA</c>/<c>BNE</c> twice.</summary>
    private const int DelayUnitCycles = Immediate + 2 * (Inherent + Branch) + Inherent + Branch;

    /// <summary>
    ///     <c>ZIREN</c> setting up the first pass: <c>LDAA</c>, <c>STAA TOP</c>, <c>LDX</c>, <c>STX SWEEP</c>,
    ///     <c>LDAA</c>, <c>LDX</c>, <c>BSR</c>.
    /// </summary>
    private const int SetUpCycles = Immediate + StoreDirect + WordImmediate + WordStoreDirect + Immediate +
                                    WordImmediate + CallShort;

    /// <summary><c>ZIREN</c> setting up the second pass: <c>LDAA</c>, <c>LDX</c>, then falling into <c>ZIREN0</c>.</summary>
    private const int SecondSetUpCycles = Immediate + WordImmediate;

    /// <summary><c>ZIREN0</c> and <c>ZIREN1</c>: <c>STAA SLOPE</c>, <c>STX END2</c>, <c>LDX #$10</c>.</summary>
    private const int PassStartCycles = StoreDirect + WordStoreDirect + WordImmediate;

    /// <summary>
    ///     <c>ZIREN2</c> after the triangle: <c>BSR</c> counted with it; then the 16-bit add (<c>LDAA</c>, <c>ADDA</c>,
    ///     <c>STAA</c>, <c>LDAA</c>, <c>ADCA</c>, <c>STAA</c>), <c>DEX</c>, <c>BNE</c>.
    /// </summary>
    private const int AddSweepCycles =
        CallShort + Direct + Direct + StoreDirect + Direct + Direct + StoreDirect + IndexStep + Branch;

    /// <summary>
    ///     <c>ZIREN5</c> and the end of a sweep: <c>LDAA</c>, <c>ADDA</c>, <c>STAA</c>, <c>BCC</c>, (<c>INC</c> on a
    ///     carry), <c>LDX</c>, <c>CPX</c>, <c>BNE</c>.
    /// </summary>
    private const int GrowSweepCycles = Direct + Direct + StoreDirect + Branch + WordDirect + WordDirect + Branch;

    /// <summary>
    ///     <c>ZIRLP1</c>/<c>ZIRLP4</c> one level: <c>STAA SOUND</c> (counted by the write), <c>ADDA</c> or <c>SUBA</c>,
    ///     <c>BCC</c>.
    /// </summary>
    private const int LevelCycles = Immediate + Branch;

    /// <summary><c>ZIRLOP</c>: <c>CLRA</c>, then <c>BSR ZIRT</c> and <c>LDAA #$E0</c> between the two halves.</summary>
    private const int TriangleStartCycles = Inherent;

    private const int TriangleMiddleCycles = CallShort + Immediate;

    /// <summary><c>ZIRT</c>: <c>LDAB TOP</c>, then each unit, then <c>RTS</c>.</summary>
    private const int DelayStartCycles = Direct;

    private readonly BoardOutput _output;
    private ushort _length = FirstLengthHigh << 8;
    private ushort _sweep = FirstSweep;

    /// <summary>Creates the sound on the board's output port.</summary>
    /// <param name="output">The board's output port.</param>
    private SirenSound(BoardOutput output)
    {
        _output = output;
    }

    /// <summary>Plays the air raid siren (sound <c>ZIREN</c>).</summary>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardOutput output)
    {
        return new SirenSound(output).Play();
    }

    private IEnumerable<OutputChange> Play()
    {
        _output.Wait(SetUpCycles);
        foreach (var change in PlayPass(FirstSlope, FirstEnd)) yield return change;

        _output.Wait(Return);
        _output.Wait(SecondSetUpCycles);
        foreach (var change in PlayPass(SecondSlope, SecondEnd)) yield return change;

        _output.Wait(Return);
    }

    /// <summary>Plays sweeps until the sweep reaches the end (<c>ZIREN0</c> to <c>ZIREN5</c>).</summary>
    /// <param name="slope">How much the sweep grows after each (<c>SLOPE</c>).</param>
    /// <param name="end">The sweep that ends the pass (<c>END2</c>).</param>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> PlayPass(byte slope, ushort end)
    {
        _output.Wait(PassStartCycles);
        do
        {
            for (var round = 0; round < RoundsPerSweep; round++)
            {
                foreach (var change in PlayTriangle()) yield return change;

                _output.Wait(AddSweepCycles);
                _length = (ushort)(_length + _sweep);
            }

            _output.Wait(GrowSweepCycles + ((_sweep & 0xFF) + slope > 0xFF ? ModifyExtended : 0));
            _sweep = (ushort)(_sweep + slope);
            _output.Wait(WordImmediate);
        } while (_sweep != end);
    }

    /// <summary>One flat triangle: up in steps of $20, a wait, down in steps of $20, a wait (<c>ZIRLOP</c>).</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> PlayTriangle()
    {
        _output.Wait(TriangleStartCycles);
        for (var level = 0; level <= 0xFF; level += TriangleStep)
        {
            yield return _output.Store((byte)level);
            _output.Wait(LevelCycles);
        }

        _output.Wait(TriangleMiddleCycles);
        WaitOneLength();
        for (var level = 0xE0; level >= 0; level -= TriangleStep)
        {
            yield return _output.Store((byte)level);
            _output.Wait(LevelCycles);
        }

        WaitOneLength();
    }

    /// <summary>Waits for the length's high byte, in units of a few instructions (<c>ZIRT</c>).</summary>
    private void WaitOneLength()
    {
        var count = (byte)(_length >> 8);
        _output.Wait(DelayStartCycles + CountdownLoop.Runs(count) * DelayUnitCycles + Return);
    }
}
