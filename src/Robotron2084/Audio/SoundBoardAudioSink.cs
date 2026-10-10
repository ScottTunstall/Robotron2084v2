using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Audio;
using Robotron2084.Tuning;

namespace Robotron2084.Audio;

/// <summary>
/// Plays the sound board through the computer's speakers, in stereo. Each port tick it runs the board
/// for one tick's worth of time and queues what the board played.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRS22.ASM</c>, routine <c>SNDOUT</c> (where the main board hands a sound
/// number to the sound board).</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$D3B6</c>.</item>
/// </list>
/// A couple of ticks of sound are kept queued so a slow frame does not leave a gap. If the game runs
/// ahead and the queue grows, the extra audio is dropped rather than letting the sound fall behind
/// the sprite.
/// </remarks>
public sealed class SoundBoardAudioSink : IAudioSink
{
    /// <summary>Samples a second sent to the speakers.</summary>
    private const int SampleRate = 44_100;

    /// <summary>The port's fixed update rate: port ticks a second.</summary>
    private const int PortTicksPerSecond = 60;

    /// <summary>Samples in one port tick. It is the size of <see cref="_monoSamples"/>, and it is multiplied by <see cref="ChannelsPerFrame"/> to give the size of <see cref="_stereoSamples"/>.</summary>
    private const int SamplesPerPortTick = SampleRate / PortTicksPerSecond;

    /// <summary>Samples per output frame: left and right. It is multiplied by <see cref="SamplesPerPortTick"/> to give the size of <see cref="_stereoSamples"/>.</summary>
    private const int ChannelsPerFrame = 2;

    /// <summary>Queued ticks of sound below which another tick is made at once, so the speakers never run dry.</summary>
    private const int MinimumQueuedTicks = 2;

    /// <summary>Queued ticks of sound at which a new tick is dropped, so the sound does not lag the screen.</summary>
    private const int MaximumQueuedTicks = 4;

    private readonly ISoundBoard _board;
    private readonly float[] _monoSamples = new float[SamplesPerPortTick];
    private readonly DynamicSoundEffectInstance _soundEffect;
    private readonly StereoPanner _panner = new();
    private readonly SoundBoardRenderer _renderer;
    private readonly short[] _stereoSamples = new short[SamplesPerPortTick * ChannelsPerFrame];

    /// <summary>Starts playing a sound board through the speakers.</summary>
    /// <param name="board">The sound board.</param>
    /// <exception cref="NoAudioHardwareException">The computer has no sound output.</exception>
    public SoundBoardAudioSink(ISoundBoard board)
    {
        _board = board;
        _renderer = new SoundBoardRenderer(board, SampleRate);
        _soundEffect = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Stereo);
        for (int tick = 0; tick < MinimumQueuedTicks; tick++)
        {
            QueueOnePortTick();
        }

        _soundEffect.Play();
    }

    /// <summary>Sends a sound number to the board and moves the sound to where it is heard.</summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    /// <param name="pan">Where the sound is heard: -1 is wholly left, 0 the middle, 1 wholly right.</param>
    public void SendSoundNumber(int soundNumber, float pan)
    {
        _panner.PanTo(pan);
        _board.SendSoundNumber(soundNumber);
    }

    /// <inheritdoc />
    public bool IsPlaying => _board.IsPlaying;

    /// <summary>Runs the board for one port tick and queues what it played, topping the queue up if it is running low.</summary>
    public void Tick()
    {
        QueueOnePortTick();
        while (_soundEffect.PendingBufferCount < MinimumQueuedTicks)
        {
            QueueOnePortTick();
        }
    }

    /// <summary>Runs the board for one port tick and queues the sound, unless the queue is already full.</summary>
    private void QueueOnePortTick()
    {
        _renderer.Render(_monoSamples);
        _panner.Spread(_monoSamples, SoundTuning.MasterVolume, _stereoSamples);
        if (_soundEffect.PendingBufferCount >= MaximumQueuedTicks)
        {
            return;
        }

        byte[] pcmBytes = MemoryMarshal.AsBytes(_stereoSamples.AsSpan()).ToArray();
        _soundEffect.SubmitBuffer(pcmBytes);
    }
}
