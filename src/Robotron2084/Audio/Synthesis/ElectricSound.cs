using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// A square wave whose pitch steps down: each round holds one pitch for a fixed stretch of time, then the
/// next round uses a slightly longer period, until the period comes round to nothing again.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>START</c>, which enters the loop <c>SND1$$</c> that
/// the source calls a 'FUNNY "ELECTRIC SOUND"' (and says it is not sure what it does).</item>
/// <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
/// </list>
/// Robotron uses it for one sound: the sixth of the eight coin sounds (sound number <c>$35</c>, through
/// <c>JMPTB1</c>). The routine's other entries, <c>SND1</c> and <c>BONUS</c>, are not reached by any
/// Robotron sound number, so they are not built.
/// </remarks>
internal sealed class ElectricSound
{
    /// <summary>The period of the first round (<c>START</c>: <c>LDAB #$11</c>).</summary>
    private const byte FirstPeriod = 0x11;

    /// <summary>What is taken off the period after each round (<c>START</c>: <c>LDAA #$FE</c>, <c>STAA FREQ2</c>). Taking $FE off is adding two.</summary>
    private const byte PeriodStep = 0xFE;

    /// <summary>The level the wave is written at (<c>SND1$$</c>: <c>LDAA #$9F</c>); the other half of the wave is its complement.</summary>
    private const byte WaveLevel = 0x9F;

    /// <summary>Counts in each round (<c>SND1A</c>: <c>LDX #$01C0</c>).</summary>
    private const int CountsPerRound = 0x01C0;

    /// <summary>The round that follows the last is not played: the sound ends once the period is no more than this (<c>CMPB #$10</c>).</summary>
    private const byte LastPeriodLimit = 0x10;

    /// <summary><c>START</c>: <c>LDAB</c>, <c>STAB SNDX1</c>, <c>LDAA</c>, <c>STAA FREQ2</c>, <c>BRA SND1$$</c>, then <c>LDAA #$9F</c>, <c>LDAB SNDX1</c>.</summary>
    private const int StartCycles = Immediate + StoreDirect + Immediate + StoreDirect + Branch + Immediate + Direct;

    /// <summary><c>SND1A</c>: <c>LDX #$01C0</c>.</summary>
    private const int StartRoundCycles = WordImmediate;

    /// <summary>One count of the round: <c>DEX</c>, <c>BEQ</c>.</summary>
    private const int CountCycles = IndexStep + Branch;

    /// <summary>Setting the period counter: <c>STAB FREQ1</c> (a full address; the source writes it as a raw <c>FCB $F7</c>).</summary>
    private const int SetPeriodCycles = StoreExtended;

    /// <summary>One count of the period: <c>DEC FREQ1</c>, <c>BNE</c> (the round's <c>DEX</c>/<c>BEQ</c> is counted separately).</summary>
    private const int PeriodCountCycles = ModifyExtended + Branch;

    /// <summary>The <c>BRA SND1B</c> that ends the second half-wave.</summary>
    private const int LoopBackCycles = Branch;

    /// <summary><c>SND1E</c> ending a round: <c>SUBB FREQ2</c>, <c>CMPB #$10</c>, <c>BHI</c>.</summary>
    private const int EndRoundCycles = Direct + Immediate + Branch;

    private readonly BoardOutput _output;
    private int _countsLeft;

    /// <summary>Creates the sound on the board's output port.</summary>
    /// <param name="output">The board's output port.</param>
    private ElectricSound(BoardOutput output) => _output = output;

    /// <summary>Plays the sixth coin sound (sound <c>START</c>).</summary>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayStart(BoardOutput output) => new ElectricSound(output).Play();

    /// <summary>Plays rounds, each a little longer in period than the last, until the period comes round (<c>SND1A</c> to <c>SND1E</c>).</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> Play()
    {
        byte period = FirstPeriod;
        _output.Wait(StartCycles);
        do
        {
            _output.Wait(StartRoundCycles);
            _countsLeft = CountsPerRound;
            while (!CountRound())
            {
                // SND1B: the first half-wave.
                _output.Wait(SetPeriodCycles);
                yield return _output.Store(WaveLevel);
                if (HoldHalfWave(period) || CountRound())
                {
                    break;
                }

                // SND1D: the second half-wave, the first's complement.
                _output.Wait(SetPeriodCycles);
                yield return _output.Complement();
                if (HoldHalfWave(period))
                {
                    break;
                }

                _output.Wait(LoopBackCycles);
            }

            _output.Wait(EndRoundCycles);
            period = (byte)(period - PeriodStep);
        }
        while (period > LastPeriodLimit);
    }

    /// <summary>Counts the round down once (<c>DEX</c>, <c>BEQ SND1E</c>).</summary>
    /// <returns>True when the round has run out.</returns>
    private bool CountRound()
    {
        _output.Wait(CountCycles);
        return --_countsLeft == 0;
    }

    /// <summary>
    /// Holds a half-wave for the period: each pass counts the round down and the period down once
    /// (<c>SND1C</c>, <c>SND1D</c>).
    /// </summary>
    /// <param name="period">The period counter's starting value (<c>FREQ1</c>).</param>
    /// <returns>True when the round ran out before the period did.</returns>
    private bool HoldHalfWave(byte period)
    {
        for (byte periodLeft = period; ; )
        {
            if (CountRound())
            {
                return true;
            }

            _output.Wait(PeriodCountCycles);
            periodLeft--;
            if (periodLeft == 0)
            {
                return false;
            }
        }
    }
}
