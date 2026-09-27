namespace Robotron2084.Audio;

/// <summary>
/// Places one channel of sound between the left and right speakers, and glides there when the place
/// changes so the move makes no click.
/// </summary>
/// <remarks>
/// The volumes follow the "constant power" rule: a sound in the middle is played at about 71% in each
/// speaker, so it sounds as loud as the same sound placed wholly in one.
/// </remarks>
public sealed class StereoPanner
{
    /// <summary>Samples per output frame: one for the left speaker, one for the right.</summary>
    private const int ChannelsPerFrame = 2;

    /// <summary>How far each sample moves the speaker volumes towards their targets: a glide of about 5 ms at 44.1 kHz.</summary>
    private const float GlidePerSample = 0.005f;

    /// <summary>A quarter turn, in radians: the angle between "all left" and "all right".</summary>
    private const float QuarterTurn = MathF.PI / 2;

    private float _left;
    private float _right;
    private float _targetLeft;
    private float _targetRight;

    /// <summary>Creates a panner with the sound in the middle.</summary>
    public StereoPanner()
    {
        PanTo(0f);
        _left = _targetLeft;
        _right = _targetRight;
    }

    /// <summary>Moves the sound to a new place between the speakers.</summary>
    /// <param name="pan">-1 is wholly left, 0 is the middle, 1 is wholly right.</param>
    public void PanTo(float pan)
    {
        float leftToRight = (Math.Clamp(pan, -1f, 1f) + 1f) / 2;
        float angle = leftToRight * QuarterTurn;
        _targetLeft = MathF.Cos(angle);
        _targetRight = MathF.Sin(angle);
    }

    /// <summary>Writes each sample to both speakers at their current volumes, as 16-bit left-right pairs.</summary>
    /// <param name="samples">The sound, one sample per slot, each from -1 to 1.</param>
    /// <param name="volume">The overall volume, from 0 to 1.</param>
    /// <param name="stereo">Where to write: two slots per sample, left then right.</param>
    public void Spread(ReadOnlySpan<float> samples, float volume, Span<short> stereo)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            _left += (_targetLeft - _left) * GlidePerSample;
            _right += (_targetRight - _right) * GlidePerSample;
            float sample = samples[i] * volume;
            int frame = i * ChannelsPerFrame;
            stereo[frame] = ToPcm(sample * _left);
            stereo[frame + 1] = ToPcm(sample * _right);
        }
    }

    /// <summary>Turns a sample from -1 to 1 into a 16-bit number, clipping anything louder.</summary>
    private static short ToPcm(float sample) => (short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue);
}
