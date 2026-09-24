namespace Robotron2084.Audio;

/// <summary>
/// Audio backend for <see cref="SoundEngine"/>. The port calls
/// <see cref="PlayNote"/> when the sequencer (re)sounds a note, and the sink
/// holds the tone for the number of port ticks that call passes.
/// <see cref="Tick"/> retires expired tones (called once per port tick).
/// </summary>
public interface IAudioSink
{
    /// <summary>Starts a tone at the given note for the given number of port ticks.</summary>
    void PlayNote(int note, int ticks);

    /// <summary>Retires the tones whose time has run out.</summary>
    void Tick();
}
