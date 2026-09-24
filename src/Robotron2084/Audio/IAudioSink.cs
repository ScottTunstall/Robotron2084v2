namespace Robotron2084.Audio;

/// <summary>
/// Audio backend for <see cref="SoundEngine"/>. The port calls
/// <see cref="PlayNote"/> when the sequencer (re)sounds a note; the sink
/// holds the tone for <paramref name="ticks"/> port ticks.
/// <see cref="Tick"/> retires expired tones (called once per port tick).
/// </summary>
public interface IAudioSink
{
    void PlayNote(int note, int ticks);
    void Tick();
}
