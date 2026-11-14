namespace Robotron2084.Audio;

/// <summary>
///     Keeps the lines and silences waiting to be spoken, and gives the voice the next line when it has finished the last.
///     A line is never cut off.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this is not in the arcade game.</item>
///         <item>
///             It does not touch the sound board. Sound effects on the board cut each other off, as in the arcade, and
///             never cut off the speech.
///         </item>
///         <item>A line asked for while another is being spoken waits its turn, in the order it was asked for.</item>
///         <item>A silence waits its turn too, then holds the next line back for as long as it lasts.</item>
///         <item>It only knows the voice it was given (<see cref="ISpeechVoice" />).</item>
///     </list>
/// </remarks>
public sealed class SpeechChannel
{
    private readonly Queue<SpeechChannelEntry> _waitingEntries = new();
    private readonly ISpeechVoice _speechVoice;
    private int _silenceTicksLeft;

    /// <summary>Makes a channel with nothing waiting.</summary>
    /// <param name="speechVoice">The voice that speaks the lines.</param>
    public SpeechChannel(ISpeechVoice speechVoice)
    {
        _speechVoice = speechVoice;
    }

    /// <summary>Gets whether a line is being spoken, a silence is being waited out, or either is waiting its turn.</summary>
    public bool IsBusy()
    {
        return _speechVoice.IsSpeaking() || _silenceTicksLeft > 0 || _waitingEntries.Count > 0;
    }

    /// <summary>Asks for a line to be spoken after anything already waiting. It never stops the line being spoken.</summary>
    /// <param name="lineName">The name of the line.</param>
    /// <param name="pitchOctaves">How far to raise or lower the line, in octaves (<see cref="ISpeechVoice.Speak" />); 0 says it as recorded.</param>
    public void Say(string lineName, float pitchOctaves = 0f)
    {
        _waitingEntries.Enqueue(new SpeechChannelEntry(lineName, pitchOctaves, 0));
    }

    /// <summary>Asks for a silence after anything already waiting, before the next line.</summary>
    /// <param name="ticks">
    ///     How long the silence lasts, in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.
    /// </param>
    public void Wait(int ticks)
    {
        _waitingEntries.Enqueue(new SpeechChannelEntry(null, 0f, ticks));
    }

    /// <summary>Counts a silence down, or gives the voice the next waiting line if it has finished the last one.</summary>
    public void Tick()
    {
        if (_silenceTicksLeft > 0 && --_silenceTicksLeft > 0) return;

        var isVoiceFree = !_speechVoice.IsSpeaking();
        var hasWaitingEntry = _waitingEntries.Count > 0;
        if (!isVoiceFree || !hasWaitingEntry) return;

        var nextEntry = _waitingEntries.Dequeue();
        if (nextEntry.LineName is not { } lineName)
        {
            _silenceTicksLeft = nextEntry.SilenceTicks;
            return;
        }

        _speechVoice.Speak(lineName, nextEntry.PitchOctaves);
    }
}
