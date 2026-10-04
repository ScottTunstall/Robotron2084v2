using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// Smoothed noise: the level slides up or down towards a random target, as fast as a limit allows, then
/// jumps to the next target. The limit shrinks every so often, so the noise grows duller and dies away.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routines <c>CANNON</c>, <c>FNLOAD</c> and <c>FNOISE</c>
/// ("FILTERED NOISE ROUTINE"), with the settings in <c>CANTB</c> ("DEFENDER SND #$17").</item>
/// <item>Disassembly: none in this repo; ROM <c>$F780</c> (<c>CANNON</c>, from the jump table <c>JMPTBL</c>).</item>
/// </list>
/// Only the cannon uses this routine in Robotron, so its settings are fixed: the slope is always
/// "distorted" by the random high byte, and it always shrinks.
/// </remarks>
internal sealed class FilteredNoise
{
    /// <summary><c>CANTB</c>'s first slope limit, high byte (<c>FMAX</c>). It is shifted up by <see cref="ByteBits"/> to give the starting value of <see cref="_limit"/>.</summary>
    private const byte CannonFirstLimit = 0xFF;

    /// <summary><c>CANTB</c>'s levels between each shrink of the limit (<c>SAMPC</c>).</summary>
    private const ushort CannonLevelsPerShrink = 0x03E8;

    /// <summary><c>CANTB</c>'s smallest slope (<c>LOFRQ</c>), added after the random distortion.</summary>
    private const byte CannonSmallestSlope = 0;

    /// <summary>The low byte of the limit at which the sound ends, once the high byte is 0 (<c>CMPB #7</c>). The low byte of <see cref="_limit"/> is compared with this to tell whether the limit has finished shrinking.</summary>
    private const byte FinalLimitLow = 7;

    /// <summary>How far the limit is shifted to get the eighth it shrinks by (three <c>LSRA</c>/<c>RORB</c> pairs). <see cref="_limit"/> is shifted down by this many bits, and the result is subtracted from <see cref="_limit"/>, so the limit shrinks.</summary>
    private const int ShrinkShift = 3;

    /// <summary>
    /// <c>CANNON</c> and <c>FNLOAD</c> loading <c>CANTB</c> (<c>LDX</c>, <c>BRA</c>, <c>LDAA</c>, <c>STAA</c>,
    /// <c>LDAA</c>, <c>STAA</c>, <c>LDAA</c>, <c>LDAB</c>, <c>LDX 4,X</c>), then <c>FNOISE</c> keeping it
    /// (<c>STAA</c>, <c>STAB</c>, <c>STX</c>, <c>CLR FLO</c>).
    /// </summary>
    private const int SetUpCycles =
        WordImmediate + Branch + Indexed + StoreDirect + Indexed + StoreDirect + Indexed + Indexed + WordIndexed
        + StoreDirect + StoreDirect + WordStoreDirect + ModifyExtended;

    /// <summary><c>FNOIS0</c> starting a stretch: <c>LDX SAMPC</c>, <c>LDAA SOUND</c>.</summary>
    private const int StartStretchCycles = WordDirect + Extended;

    /// <summary>
    /// Choosing this slide's slope: <c>LDAB FMAX</c>, <c>TST DSFLG</c>, <c>BEQ</c>, <c>ANDB HI</c>,
    /// <c>ADDB LOFRQ</c>, then <c>STAB FHI</c>, <c>LDAB FLO</c>, <c>CMPA LO</c>, <c>BHI</c>.
    /// </summary>
    private const int ChooseSlopeCycles =
        Direct + ModifyExtended + Branch + Direct + Direct + StoreDirect + Direct + Direct + Branch;

    /// <summary>Counting a level: <c>DEX</c>, <c>BEQ</c>.</summary>
    private const int CountLevelCycles = IndexStep + Branch;

    /// <summary>Sliding one step: <c>ADDB</c>/<c>SUBB FLO</c>, <c>ADCA</c>/<c>SBCA FHI</c>, <c>BCS</c>.</summary>
    private const int SlideCycles = Direct + Direct + Branch;

    /// <summary>Checking whether the target is reached: <c>CMPA LO</c> and a branch.</summary>
    private const int CheckTargetCycles = Direct + Branch;

    /// <summary>
    /// <c>FNOIS6</c> shrinking the limit by an eighth: <c>LDAB FDFLG</c>, <c>BEQ</c>, <c>LDAA</c>, <c>LDAB</c>,
    /// three <c>LSRA</c>/<c>RORB</c>, <c>COMA</c>, <c>NEGB</c>, <c>SBCA</c>, <c>ADDB</c>, <c>ADCA</c>,
    /// <c>STAB</c>, <c>STAA</c>, <c>BNE</c>.
    /// </summary>
    private const int ShrinkCycles =
        Direct + Branch + Direct + Direct + (ShrinkShift * 2 * Inherent) + Inherent + Inherent + Immediate
        + Direct + Direct + StoreDirect + StoreDirect + Branch;

    /// <summary>The bits of a byte. It is used to shift <see cref="_limit"/>, both to set its starting value and to read its top byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a 16-bit sum that stay in it.</summary>
    private const int WordMask = 0xFFFF;

    private readonly BoardMemory _memory;
    private readonly BoardOutput _output;
    private ushort _limit = CannonFirstLimit << ByteBits;
    private ushort _levelsLeft;
    private byte _level;

    /// <summary>Creates the sound on the board's memory and output port.</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    private FilteredNoise(BoardMemory memory, BoardOutput output)
    {
        _memory = memory;
        _output = output;
    }

    /// <summary>Plays the cannon sound (sound <c>CANNON</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayCannon(BoardMemory memory, BoardOutput output)
    {
        output.Wait(SetUpCycles);
        return new FilteredNoise(memory, output).Play();
    }

    /// <summary>Plays stretches of slides, shrinking the limit after each, until it reaches its end (<c>FNOIS0</c>).</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> Play()
    {
        do
        {
            _output.Wait(StartStretchCycles);
            _levelsLeft = CannonLevelsPerShrink;
            _level = _output.Level;
            bool hasLevelsLeft;
            do
            {
                foreach (OutputChange change in SlideToTarget())
                {
                    yield return change;
                }

                hasLevelsLeft = _levelsLeft != 0;
            }
            while (hasLevelsLeft);
        }
        while (ShrinkLimit());
    }

    /// <summary>
    /// Picks a random target and slides the level towards it one step per level, then jumps to it
    /// (<c>FNOIS1</c> to <c>FNOIS5</c>); stops early when the stretch runs out of levels.
    /// </summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> SlideToTarget()
    {
        foreach (OutputChange change in RandomStep.Run(_memory, _output, _level, RandomStep.MixLevelCycles))
        {
            yield return change;
        }

        _output.Wait(ChooseSlopeCycles);
        var slopeHigh = (byte)(((_limit >> ByteBits) & _memory.RandomHigh) + CannonSmallestSlope);
        int slope = (slopeHigh << ByteBits) | (byte)_limit;
        int position = (_level << ByteBits) | (byte)_limit;
        bool isFalling = _level > _memory.RandomLow;
        while (true)
        {
            _output.Wait(CountLevelCycles);
            _levelsLeft--;
            if (_levelsLeft == 0)
            {
                yield break;
            }

            yield return _output.Store(_level);
            _output.Wait(SlideCycles);
            int next = isFalling ? position - slope : position + slope;
            if (next < 0 || next > WordMask)
            {
                break;
            }

            position = next;
            _level = (byte)(position >> ByteBits);
            _output.Wait(CheckTargetCycles);
            if (isFalling != _level > _memory.RandomLow)
            {
                if (!isFalling)
                {
                    _output.Wait(Branch);
                }

                break;
            }
        }

        _output.Wait(Direct);
        _level = _memory.RandomLow;
        yield return _output.Store(_level);
        _output.Wait(Branch);
    }

    /// <summary>Takes an eighth off the slope limit (<c>FNOIS6</c>).</summary>
    /// <returns>True to play another stretch; false once the limit has shrunk to its end.</returns>
    private bool ShrinkLimit()
    {
        _output.Wait(ShrinkCycles);
        _limit = (ushort)(_limit - (_limit >> ShrinkShift));
        if ((_limit >> ByteBits) != 0)
        {
            return true;
        }

        _output.Wait(Immediate + Branch);
        return (byte)_limit != FinalLimitLow;
    }
}
