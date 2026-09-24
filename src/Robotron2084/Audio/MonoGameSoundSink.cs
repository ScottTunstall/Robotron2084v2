using Microsoft.Xna.Framework.Audio;

namespace Robotron2084.Audio;

/// <summary>
/// MonoGame <see cref="IAudioSink"/>: 8-bit mono square-wave tones.
/// NOTE — the real note→frequency map is sound-board hardware (not in the
/// CPU ROM, notes §36.2) and is not decoded yet. Until
/// then this sink uses a stub equal-temperament scale (110 Hz base, 2^(n/12))
/// — a musically plausible stand-in, deliberately NOT arcade-faithful.
/// </summary>
public sealed class MonoGameSoundSink : IAudioSink
{
    private const int SampleRate = 22050;
    private const int LoopSeconds = 1;
    private const float MasterVolume = 0.6f;

    private readonly Dictionary<int, SoundEffect> _toneCache = new();
    private SoundEffectInstance? _current;
    private int _currentTicksLeft;

    public void PlayNote(int note, int ticks)
    {
        _current?.Stop();
        _current = GetTone(note).CreateInstance();
        _current.Volume = MasterVolume;
        _current.IsLooped = true;
        _current.Play();
        _currentTicksLeft = Math.Max(1, ticks);
    }

    public void Tick()
    {
        if (_current is null)
        {
            return;
        }

        if (--_currentTicksLeft <= 0)
        {
            _current.Stop();
            _current = null;
        }
    }

    /// <summary>
    /// Stub scale (NOT the arcade table): note n → 110 × 2^(n/12) Hz.
    /// The decoded ROM call sites use notes 0x01..0x25, which land at
    /// ~117 Hz..~1.5 kHz — square waves in the arcade's ballpark.
    /// </summary>
    internal static double StubFrequency(int note) => 110.0 * Math.Pow(2, note / 12.0);

    private SoundEffect GetTone(int note)
    {
        if (_toneCache.TryGetValue(note, out SoundEffect? cached))
        {
            return cached;
        }

        SoundEffect tone = CreateSquareTone((float)StubFrequency(note));
        _toneCache[note] = tone;
        return tone;
    }

    private static SoundEffect CreateSquareTone(float hertz)
    {
        int samples = SampleRate * LoopSeconds;
        byte[] data = new byte[samples];
        int period = Math.Max(2, (int)Math.Round(SampleRate / hertz));
        int fade = SampleRate / 100; // 10 ms fade in/out (kills the click)

        for (int i = 0; i < samples; i++)
        {
            // 50% duty square: 0x80/0xFF around the 128 midline.
            byte level = (i / period) % 2 == 0 ? (byte)0xFF : (byte)0x80;
            int env = i < fade ? i * 127 / fade
                : i >= samples - fade ? (samples - 1 - i) * 127 / fade
                : 127;
            data[i] = (byte)(128 + ((level - 128) * env) / 127);
        }

        return new SoundEffect(data, SampleRate, AudioChannels.Mono);
    }
}
