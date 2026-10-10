using Robotron2084.Audio;

namespace Robotron2084.Tests.Audio;

/// <summary>A sink for sequencer tests: records each sound number sent, where it is heard, and the tick it went out on.</summary>
internal sealed class RecordingSink : IAudioSink
{
    private int _tick;

    /// <summary>Every sound number sent, in order, with its pan and the tick (counting from 1) it was sent on.</summary>
    public List<(int SoundNumber, float Pan, int Tick)> Sends { get; } = [];

    public void SendSoundNumber(int soundNumber, float pan)
    {
        Sends.Add((soundNumber, pan, _tick));
    }

    public void Tick()
    {
        _tick++;
    }

    /// <summary>The sound numbers sent, in order.</summary>
    public List<int> SoundNumbers()
    {
        return [.. Sends.Select(send => send.SoundNumber)];
    }

    /// <summary>The ticks the sound numbers went out on, in order.</summary>
    public List<int> SendTicks()
    {
        return [.. Sends.Select(send => send.Tick)];
    }
}
