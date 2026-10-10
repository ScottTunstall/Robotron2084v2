using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The board's square wave with separate low and high times: every so often both times grow, which
///     sweeps the pitch, until the high time reaches an end value.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c>, routines <c>VARILD</c> ("VARI LOADER") and <c>VARI</c>
///             ("VARIABLE DUTY CYCLE SQUARE WAVE ROUTINE"), and the table <c>VVECT</c> (ROM <c>$FC08</c>).
///         </item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
/// </remarks>
internal sealed class SquareWaveSound
{
    /// <summary>
    ///     The place of <c>CABSHK</c>, the spinner sound's settings, in the settings table (<c>SP1</c>:
    ///     <c>LDAA #(CABSHK-VVECT)/9</c>).
    /// </summary>
    public const int CabinetShakeVector = 3;

    /// <summary>
    ///     <c>VARILD</c>: find the settings (<c>TAB</c>, three <c>ASLA</c>, <c>ABA</c>, <c>LDX</c>, <c>STX XPTR</c>,
    ///     <c>LDX #VVECT</c>, <c>JSR ADDX</c>), then copy all nine bytes (<c>LDAB</c>, <c>JMP TRANS</c>).
    /// </summary>
    private const int LoadCycles =
        Inherent + 3 * Inherent + Inherent + WordImmediate + WordStoreDirect + WordImmediate +
        SubroutineCycles.CallAddToIndex
        + Immediate + JumpExtended + SubroutineCycles.Transfer + VectorBytes * SubroutineCycles.TransferPerByte;

    /// <summary>How many bytes a <see cref="SquareWaveVector" /> takes in the ROM.</summary>
    private const int VectorBytes = 9;

    /// <summary><c>VAR0</c> starting both counts: <c>LDAA</c>, <c>STAA</c>, <c>LDAA</c>, <c>STAA</c>.</summary>
    private const int StartCountsCycles = 2 * Direct + 2 * StoreDirect;

    /// <summary>One count of the low or high time: <c>DEX</c>, <c>BEQ</c>, <c>DECA</c>, <c>BNE</c>.</summary>
    private const int CountCycles = IndexStep + Branch + Inherent + Branch;

    /// <summary>The part of a count that runs before the sweep check can leave the loop: <c>DEX</c>, <c>BEQ</c>.</summary>
    private const int SweepCheckCycles = IndexStep + Branch;

    /// <summary><c>VSWEEP</c> reading the port: <c>LDAA SOUND</c>, <c>BMI</c>.</summary>
    private const int ReadPortCycles = Extended + Branch;

    /// <summary><c>VSWEEP</c> growing the low count: <c>LDAA</c>, <c>ADDA</c>, <c>STAA</c>.</summary>
    private const int GrowCountCycles = Direct + Direct + StoreDirect;

    /// <summary>Comparing the high count with its end and branching: <c>CMPA HIEN</c>, <c>BNE</c>.</summary>
    private const int CheckEndCycles = Direct + Branch;

    /// <summary>Changing the starting low time and branching on it: <c>ADDA LOPER</c>, <c>STAA LOPER</c>, <c>BNE</c>.</summary>
    private const int ChangeLowWaitCycles = Direct + StoreDirect + Branch;

    /// <summary>The bit that marks a high level.</summary>
    private const int HighBit = 0x80;

    /// <summary>
    ///     <c>VVECT</c>: every square wave sound's settings. A sound number picks one by its place, counting from 0.
    /// </summary>
    private static readonly SquareWaveVector[] Vectors =
    [
        new(0x40, 0x01, 0x00, 0x10, 0xE1, 0x0080, 0xFF, 0xFF), // SAW
        new(0x28, 0x01, 0x00, 0x08, 0x81, 0x0200, 0xFF, 0xFF), // FOSHIT
        new(0x28, 0x81, 0x00, 0xFC, 0x01, 0x0200, 0xFC, 0xFF), // QUASAR
        new(0xFF, 0x01, 0x00, 0x18, 0x41, 0x0480, 0x00, 0xFF), // CABSHK
        new(0x00, 0xFF, 0x08, 0xFF, 0x68, 0x0480, 0x00, 0xFF), // CSCALE
        new(0x28, 0x81, 0x00, 0xFC, 0x01, 0x0200, 0xFC, 0xFF), // MOSQTO
        new(0x60, 0x01, 0x57, 0x08, 0xE1, 0x0200, 0xFE, 0x80) // VARBG1
    ];

    private readonly BoardOutput _output;
    private byte _highCount;
    private byte _lowCount;
    private ushort _sweepCountsLeft;
    private SquareWaveVector _vector;

    /// <summary>Creates the sound on the board's output port.</summary>
    /// <param name="output">The board's output port.</param>
    public SquareWaveSound(BoardOutput output)
    {
        _output = output;
    }

    /// <summary>How long the wave stays low each time at the start of each sweep, in counts (<c>LOPER</c>).</summary>
    public byte LowWait
    {
        get => _vector.LowWait;
        set => _vector = _vector with { LowWait = value };
    }

    /// <summary>Copies a sound's settings into place (<c>VARILD</c>).</summary>
    /// <param name="vectorIndex">The sound's place in the settings table.</param>
    public void Load(int vectorIndex)
    {
        _output.Wait(LoadCycles);
        _vector = Vectors[vectorIndex];
    }

    /// <summary>Plays the loaded sound until its sweeps end (<c>VARI</c>); some never end.</summary>
    /// <returns>The sound's output changes.</returns>
    public IEnumerable<OutputChange> Play()
    {
        _output.Wait(Direct);
        yield return _output.Store(_vector.Level);
        do
        {
            _output.Wait(StartCountsCycles);
            _lowCount = _vector.LowWait;
            _highCount = _vector.HighWait;
            do
            {
                foreach (var change in PlayUntilSweep()) yield return change;

                foreach (var change in Sweep()) yield return change;
            } while (_highCount != _vector.HighWaitEnd);
        } while (ChangeLowWait());
    }

    /// <summary>Plays low then high, over and over, until the sweep count runs out (<c>V0</c>, <c>V0LP</c>).</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> PlayUntilSweep()
    {
        _output.Wait(WordDirect);
        _sweepCountsLeft = _vector.SweepLength;
        while (true)
        {
            _output.Wait(Direct);
            yield return _output.Complement();
            if (!CountDown(_lowCount)) yield break;

            yield return _output.Complement();
            _output.Wait(Direct);
            if (!CountDown(_highCount)) yield break;

            _output.Wait(Branch);
        }
    }

    /// <summary>Counts one low or high time down, alongside the sweep count (<c>V1</c>, <c>V2</c>).</summary>
    /// <param name="count">The time, in counts; 0 means 256.</param>
    /// <returns>True when the time ran out; false when the sweep count ran out first.</returns>
    private bool CountDown(byte count)
    {
        while (true)
        {
            _output.Wait(SweepCheckCycles);
            _sweepCountsLeft--;
            if (_sweepCountsLeft == 0) return false;

            _output.Wait(CountCycles - SweepCheckCycles);
            count--;
            if (count == 0) return true;
        }
    }

    /// <summary>Makes the level high, then grows both times (<c>VSWEEP</c>).</summary>
    /// <returns>The output change.</returns>
    private IEnumerable<OutputChange> Sweep()
    {
        _output.Wait(ReadPortCycles);
        var level = _output.Level;
        if (level < HighBit)
        {
            _output.Wait(Inherent);
            level = (byte)~level;
        }

        _output.Wait(Immediate);
        yield return _output.Store(level);
        _output.Wait(GrowCountCycles + GrowCountCycles + CheckEndCycles);
        _lowCount += _vector.LowWaitStep;
        _highCount += _vector.HighWaitStep;
    }

    /// <summary>
    ///     When the sweeps end, changes the starting low time and says whether to play again: not when there
    ///     is no change, or when the low time comes round to 0 (<c>LDAA LOMOD</c> to <c>VARX</c>).
    /// </summary>
    /// <returns>True to play again from the new low time.</returns>
    private bool ChangeLowWait()
    {
        _output.Wait(Direct + Branch);
        if (_vector.LowWaitChange == 0)
        {
            _output.Wait(Return);
            return false;
        }

        _output.Wait(ChangeLowWaitCycles);
        LowWait += _vector.LowWaitChange;
        return LowWait != 0;
    }
}
