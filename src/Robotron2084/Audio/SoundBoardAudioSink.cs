using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Audio;
using Robotron2084.Tuning;

namespace Robotron2084.Audio;

/// <summary>
///     Plays the sound board through the computer's speakers, in stereo. Each port tick it runs the board
///     for one tick's worth of time and queues what the board played.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRS22.ASM</c>, routine <c>SNDOUT</c> (where the main board hands a sound
///             number to the sound board).
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$D3B6</c>.</item>
///     </list>
///     A couple of ticks of sound are kept queued so a slow frame does not leave a gap. If the game runs
///     ahead and the queue grows, the extra audio is dropped rather than letting the sound fall behind
///     the sprite.
/// </remarks>
public sealed class SoundBoardAudioSink : IAudioSink
{
    /// <summary>
    ///     Samples per output frame: left and right. It is multiplied by <see cref="SamplesPerPortTick" /> to give the
    ///     size of <see cref="_stereoSamples" />.
    /// </summary>
    private const int ChannelsPerFrame = 2;

    /// <summary>Queued ticks of sound at which a new tick is dropped, so the sound does not lag the screen.</summary>
    private const int MaximumQueuedTicks = 4;

    /// <summary>Queued ticks of sound below which another tick is made at once, so the speakers never run dry.</summary>
    private const int MinimumQueuedTicks = 2;

    /// <summary>Samples a second sent to the speakers.</summary>
    private const int SampleRate = 44_100;

    /// <summary>
    ///     Samples in one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. It is the size of
    ///     <see cref="_monoSamples" />, and it is multiplied by <see cref="ChannelsPerFrame" /> to give the size of
    ///     <see cref="_stereoSamples" />.
    /// </summary>
    private const int SamplesPerPortTick = SampleRate / Robotron2084.Core.ArcadeClock.PortTicksPerSecond;

    private readonly ISoundBoard _soundBoard;
    private readonly float[] _monoSamples = new float[SamplesPerPortTick];
    private readonly StereoPanner _stereoPanner = new();
    private readonly SoundBoardRenderer _soundBoardRenderer;
    private readonly DynamicSoundEffectInstance _dynamicSoundEffectInstance;
    private readonly short[] _stereoSamples = new short[SamplesPerPortTick * ChannelsPerFrame];

    /// <summary>Starts playing a sound board through the speakers.</summary>
    /// <param name="soundBoard">The sound board.</param>
    /// <exception cref="NoAudioHardwareException">The computer has no sound output.</exception>
    public SoundBoardAudioSink(ISoundBoard soundBoard)
    {
        _soundBoard = soundBoard;
        _soundBoardRenderer = new SoundBoardRenderer(soundBoard, SampleRate);
        _dynamicSoundEffectInstance = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Stereo);
        for (var tick = 0; tick < MinimumQueuedTicks; tick++) QueueOnePortTick();

        _dynamicSoundEffectInstance.Play();
    }

    /// <inheritdoc />
    public bool IsPlaying() => _soundBoard.IsPlaying();

    /// <summary>Sends a sound number to the board and moves the sound to where it is heard.</summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    /// <param name="pan">Where the sound is heard: -1 is wholly left, 0 the middle, 1 wholly right.</param>
    public void SendSoundNumber(int soundNumber, float pan)
    {
        _stereoPanner.PanTo(pan);
        _soundBoard.SendSoundNumber(soundNumber);
    }

    /// <summary>Runs the board for one port tick and queues what it played, topping the queue up if it is running low.</summary>
    public void Tick()
    {
        QueueOnePortTick();
        while (_dynamicSoundEffectInstance.PendingBufferCount < MinimumQueuedTicks) QueueOnePortTick();
    }

    /// <summary>Runs the board for one port tick and queues the sound, unless the queue is already full.</summary>
    private void QueueOnePortTick()
    {
        _soundBoardRenderer.Render(_monoSamples);
        _stereoPanner.Spread(_monoSamples, SoundTuning.MasterVolume, _stereoSamples);
        if (_dynamicSoundEffectInstance.PendingBufferCount >= MaximumQueuedTicks) return;

        var pcmBytes = MemoryMarshal.AsBytes(_stereoSamples.AsSpan()).ToArray();
        _dynamicSoundEffectInstance.SubmitBuffer(pcmBytes);
    }
}
