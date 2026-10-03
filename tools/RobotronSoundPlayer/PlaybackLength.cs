namespace RobotronSoundPlayer;

/// <summary>
/// Decides when to stop: after a set time when one is given, otherwise once the sound has been quiet for
/// a while, or at a limit for the sounds that never stop by themselves.
/// </summary>
internal sealed class PlaybackLength
{
    /// <summary>The longest a sound plays when no time is given, in seconds.</summary>
    public const double LimitSeconds = 15;

    /// <summary>Loudness below which a sample counts as silence (the measure <c>SoundBoardRomTests</c> uses).</summary>
    private const float Silence = 0.01f;

    /// <summary>How long a sound must stay quiet before it counts as finished, in port ticks: three quarters of a second.</summary>
    private const int QuietTicksToFinish = PlayerAudio.PortTicksPerSecond * 3 / 4;

    private readonly bool _isTimed;
    private readonly int _limitTicks;
    private int _quietTicks;
    private int _ticks;

    /// <summary>Sets how long to play.</summary>
    /// <param name="seconds">Play exactly this long, or null to stop when the sound goes quiet.</param>
    public PlaybackLength(double? seconds)
    {
        _isTimed = seconds is not null;
        _limitTicks = (int)Math.Ceiling((seconds ?? LimitSeconds) * PlayerAudio.PortTicksPerSecond);
    }

    /// <summary>True once playing should stop.</summary>
    public bool IsOver => FellSilent || ReachedLimit;

    /// <summary>True when the sound stopped because it went quiet.</summary>
    public bool FellSilent => !_isTimed && _quietTicks >= QuietTicksToFinish;

    /// <summary>True when the sound stopped because its time ran out.</summary>
    public bool ReachedLimit => _ticks >= _limitTicks;

    /// <summary>How long the sound lasted, in seconds, not counting the quiet at the end.</summary>
    public double HeardSeconds => (double)(_ticks - (FellSilent ? _quietTicks : 0)) / PlayerAudio.PortTicksPerSecond;

    /// <summary>Counts one more port tick of sound.</summary>
    /// <param name="samples">What was heard during it.</param>
    public void Count(ReadOnlySpan<float> samples)
    {
        _ticks++;
        _quietTicks = IsQuiet(samples) ? _quietTicks + 1 : 0;
    }

    /// <summary>True when every sample is below the silence level.</summary>
    /// <param name="samples">The samples.</param>
    private static bool IsQuiet(ReadOnlySpan<float> samples)
    {
        foreach (float sample in samples)
        {
            if (Math.Abs(sample) >= Silence)
            {
                return false;
            }
        }

        return true;
    }
}
