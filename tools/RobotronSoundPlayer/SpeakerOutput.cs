using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Robotron2084.Tuning;

namespace RobotronSoundPlayer;

/// <summary>Plays a sound through the computer's speakers with MonoGame, until it ends or a key is pressed.</summary>
internal static class SpeakerOutput
{
    /// <summary>Port ticks of sound kept queued, so the speakers never run dry.</summary>
    private const int QueuedTicks = 4;

    /// <summary>How long to wait between topping the queue up, in milliseconds.</summary>
    private const int PollMilliseconds = 5;

    /// <summary>Plays a sound until its length says to stop, or until a key is pressed.</summary>
    /// <param name="source">Makes the sound.</param>
    /// <param name="length">Says when to stop.</param>
    /// <returns>True when a key stopped it early.</returns>
    /// <exception cref="NoAudioHardwareException">The computer has no sound output.</exception>
    public static bool Play(ISoundSource source, PlaybackLength length)
    {
        using var output = new DynamicSoundEffectInstance(PlayerAudio.SampleRate, AudioChannels.Mono);
        var samples = new float[PlayerAudio.SamplesPerPortTick];
        output.Play();
        while (!length.IsOver)
        {
            if (WasKeyPressed()) return true;

            TopUp(output, source, length, samples);
            Thread.Sleep(PollMilliseconds);
        }

        return WaitForQueueToEmpty(output);
    }

    /// <summary>Queues port ticks of sound until the queue is full or the sound has ended.</summary>
    private static void TopUp(DynamicSoundEffectInstance output, ISoundSource source, PlaybackLength length,
        float[] samples)
    {
        FrameworkDispatcher.Update();
        while (output.PendingBufferCount < QueuedTicks && !length.IsOver)
        {
            source.RenderPortTick(samples);
            length.Count(samples);
            output.SubmitBuffer(PlayerAudio.ToPcm16(samples, SoundTuning.MasterVolume));
        }
    }

    /// <summary>Lets the sound already queued play out, unless a key is pressed.</summary>
    /// <returns>True when a key stopped it early.</returns>
    private static bool WaitForQueueToEmpty(DynamicSoundEffectInstance output)
    {
        while (output.PendingBufferCount > 0)
        {
            if (WasKeyPressed()) return true;

            FrameworkDispatcher.Update();
            Thread.Sleep(PollMilliseconds);
        }

        return false;
    }

    /// <summary>True when a key has been pressed in the console; the key is used up.</summary>
    private static bool WasKeyPressed()
    {
        if (Console.IsInputRedirected || !Console.KeyAvailable) return false;

        Console.ReadKey(true);
        return true;
    }
}
