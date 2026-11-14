
namespace Robotron2084.Audio;

/// <summary>
///     How loud the sound board plays while the speech is or is not talking. The board is turned down while the speech
///     talks, so the words are louder than the game's sounds, and turned back up when it stops. The volume slides there a
///     step each tick instead of jumping, so the change makes no click. Port only.
/// </summary>
public sealed class BoardVolumeDuringSpeech
{
    /// <summary>The board's volume when the speech is quiet: the board as loud as it is set to be.</summary>
    private const float FullVolume = 1f;

    /// <summary>
    ///     The share of the board's volume to play at, from <see cref="SoundTuning.BoardVolumeWhileSpeaking" /> to 1. It
    ///     starts at 1.
    /// </summary>
    public float Volume { get; private set; } = FullVolume;

    /// <summary>Moves the volume one tick's step towards where it should be.</summary>
    /// <param name="isSpeechTalking">True while the speech is saying something, or waiting to say the next part of it.</param>
    public void Advance(bool isSpeechTalking)
    {
        var target = isSpeechTalking ? SoundTuning.BoardVolumeWhileSpeaking : FullVolume;
        var step = Math.Clamp(target - Volume, -SoundTuning.BoardVolumeStepPerTick, SoundTuning.BoardVolumeStepPerTick);
        Volume += step;
    }
}
