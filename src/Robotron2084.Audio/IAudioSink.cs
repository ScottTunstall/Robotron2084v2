namespace Robotron2084.Audio;

/// <summary>Something that plays sounds, chosen by sound number.</summary>
public interface IAudioSink
{
    /// <summary>True while a sound is still being played: sent and not yet over.</summary>
    bool IsPlaying();

    /// <summary>Starts the sound with this number, heard from this place between the speakers.</summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    /// <param name="pan">Where the sound is heard: -1 is wholly left, 0 the middle, 1 wholly right.</param>
    void SendSoundNumber(int soundNumber, float pan);

    /// <summary>Moves the sound on by one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    void Tick();
}
