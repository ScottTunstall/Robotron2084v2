using Robotron2084.Audio.Synthesis;

namespace Robotron2084.Audio;

/// <summary>
///     Turns a sound board's output into audio samples, by running the board for exactly as long as
///     each sample lasts and averaging its output level over that time.
/// </summary>
/// <remarks>
///     The arcade's amplifier is joined to the board through a capacitor, which lets the sound through
///     but not a steady level. <see cref="RemoveSteadyLevel" /> does the same job, so a board resting at
///     any level is silent.
/// </remarks>
public sealed class SoundBoardRenderer
{
    /// <summary>
    ///     The output level in the middle of the board's 0 to 255 range. It is subtracted from <see cref="_level" />, and
    ///     the result is divided by it, to set <see cref="_previousInput" />.
    /// </summary>
    private const float MidLevel = 128f;

    /// <summary>
    ///     How much of its last output the steady-level filter keeps each sample. Close to 1, so it only
    ///     removes changes slower than about 20 times a second.
    ///     It is multiplied by <see cref="_previousOutput" /> and the result is added in to work out the next output.
    /// </summary>
    private const float SteadyLevelRetention = 0.997f;

    /// <summary>How long the board runs before the first sample, so its start-up settles out of earshot: a tenth of a second.</summary>
    private const int WarmUpCycles = SoundBoard.ClockHertz / 10;

    private readonly ISoundBoard _board;
    private readonly double _cyclesPerSample;
    private byte _level;
    private float _previousInput;
    private float _previousOutput;
    private double _unspentCycles;

    /// <summary>Creates a renderer for a board, and runs the board's start-up.</summary>
    /// <param name="board">The sound board.</param>
    /// <param name="sampleRate">Samples a second.</param>
    public SoundBoardRenderer(ISoundBoard board, int sampleRate)
    {
        _board = board;
        _cyclesPerSample = (double)SoundBoard.ClockHertz / sampleRate;
        WarmUp();
    }

    /// <summary>Fills a buffer with the board's sound, one sample per slot, each from -1 to 1.</summary>
    /// <param name="samples">The buffer to fill.</param>
    public void Render(Span<float> samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var centredLevel = (AverageLevelOverOneSample() - MidLevel) / MidLevel;
            samples[i] = RemoveSteadyLevel(centredLevel);
        }
    }

    /// <summary>
    ///     Runs the board for one sample's worth of time and averages its output level. A level holds while
    ///     the board runs towards its next change, and the new level counts from the moment it is made.
    /// </summary>
    /// <returns>The average level, 0 to 255.</returns>
    private float AverageLevelOverOneSample()
    {
        var cyclesLeft = _cyclesPerSample;
        double weightedLevels = 0;
        while (cyclesLeft > 0)
        {
            if (_unspentCycles <= 0)
            {
                _level = _board.OutputLevel;
                _unspentCycles += _board.Run((int)Math.Ceiling(cyclesLeft));
            }

            var cycles = Math.Min(cyclesLeft, _unspentCycles);
            weightedLevels += cycles * _level;
            cyclesLeft -= cycles;
            _unspentCycles -= cycles;
        }

        return (float)(weightedLevels / _cyclesPerSample);
    }

    /// <summary>Lets the sound through but not a steady level, as the arcade's coupling capacitor does.</summary>
    /// <param name="input">This sample, before the filter.</param>
    /// <returns>This sample, after it.</returns>
    private float RemoveSteadyLevel(float input)
    {
        var output = input - _previousInput + SteadyLevelRetention * _previousOutput;
        _previousInput = input;
        _previousOutput = output;
        return output;
    }

    /// <summary>Runs the board's start-up, then starts the filter from the level the board settled on.</summary>
    private void WarmUp()
    {
        for (var cycles = 0; cycles < WarmUpCycles;) cycles += _board.Run(WarmUpCycles - cycles);

        _level = _board.OutputLevel;
        _previousInput = (_level - MidLevel) / MidLevel;
    }
}
