using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>A voice that speaks each line for a set number of ticks, and records what it was asked to speak.</summary>
internal sealed class FakeSpeechVoice : ISpeechVoice
{
    private int _ticksLeft;

    /// <summary>The lines it was asked to speak, in order.</summary>
    public List<string> SpokenLineNames { get; } = [];

    /// <summary>How far each spoken line was raised or lowered, in octaves, in the order spoken.</summary>
    public List<float> SpokenPitchOctaves { get; } = [];

    /// <summary>How many ticks each line takes to speak.</summary>
    public int TicksPerLine { get; init; } = 3;

    /// <inheritdoc />
    public bool IsSpeaking()
    {
        return _ticksLeft > 0;
    }

    /// <inheritdoc />
    public void Speak(string lineName, float pitchOctaves)
    {
        Assert.Equal(0, _ticksLeft); // the channel must never start a line over a line being spoken
        SpokenLineNames.Add(lineName);
        SpokenPitchOctaves.Add(pitchOctaves);
        _ticksLeft = TicksPerLine;
    }

    /// <summary>Lets one tick of the line pass.</summary>
    public void Tick()
    {
        if (_ticksLeft > 0) _ticksLeft--;
    }
}
