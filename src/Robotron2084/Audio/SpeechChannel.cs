using Robotron2084.Audio.Models;

namespace Robotron2084.Audio;

/// <summary>
///     Keeps the lines and silences waiting to be spoken, and gives the voice the next line when it has finished the last.
///     A line is only cut off by one that matters more (<see cref="SpeechPriority" />).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this is not in the arcade game.</item>
///         <item>
///             It does not touch the sound board. Sound effects on the board cut each other off, as in the arcade, and
///             never cut off the speech.
///         </item>
///         <item>
///             A line asked for while another is being spoken waits its turn, in the order it was asked for, unless it
///             matters more than what is being said. Then what is being said stops at once, what is waiting is dropped,
///             and the new line starts on the next tick.
///         </item>
///         <item>A silence waits its turn too, then holds the next line back for as long as it lasts.</item>
///         <item>It only knows the voice it was given (<see cref="ISpeechVoice" />).</item>
///     </list>
/// </remarks>
public sealed class SpeechChannel
{
    private readonly ISpeechVoice _speechVoice;
    private readonly Queue<SpeechChannelEntry> _waitingEntries = new();
    private int _silenceTicksLeft;

    /// <summary>How much what is being said, or waiting to be said, matters. It is only kept while the channel is busy.</summary>
    private SpeechPriority _speechPriority = SpeechPriority.Normal;

    /// <summary>Makes a channel with nothing waiting.</summary>
    /// <param name="speechVoice">The voice that speaks the lines.</param>
    /// <exception cref="ArgumentNullException">The voice is null.</exception>
    public SpeechChannel(ISpeechVoice speechVoice)
    {
        ArgumentNullException.ThrowIfNull(speechVoice);

        _speechVoice = speechVoice;
    }

    /// <summary>Gets whether a line is being spoken, a silence is being waited out, or either is waiting its turn.</summary>
    public bool IsBusy()
    {
        return _speechVoice.IsSpeaking() || _silenceTicksLeft > 0 || _waitingEntries.Count > 0;
    }

    /// <summary>
    ///     Asks for a line to be spoken after anything already waiting. A line that matters more than what is being said
    ///     stops it and drops what is waiting instead.
    /// </summary>
    /// <param name="lineName">The name of the line.</param>
    /// <param name="pitchOctaves">How far to raise or lower the line, in octaves (<see cref="ISpeechVoice.Speak" />); 0 says it as recorded.</param>
    /// <param name="volumeShare">How loud the line is, as a share of the speech's volume (<see cref="ISpeechVoice.Speak" />); 1 is full.</param>
    /// <param name="speechPriority">How much the line matters.</param>
    /// <exception cref="ArgumentException">The name is null or empty.</exception>
    public void Say(string lineName, float pitchOctaves = 0f, float volumeShare = 1f,
        SpeechPriority speechPriority = SpeechPriority.Normal)
    {
        ArgumentException.ThrowIfNullOrEmpty(lineName);

        MakeWayFor(speechPriority);
        _waitingEntries.Enqueue(new SpeechChannelEntry(lineName, pitchOctaves, volumeShare, 0));
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

        _speechVoice.Speak(lineName, nextEntry.PitchOctaves, nextEntry.VolumeShare);
    }

    /// <summary>
    ///     Asks for a silence after anything already waiting, before the next line. A silence that matters more than what
    ///     is being said stops it and drops what is waiting instead.
    /// </summary>
    /// <param name="ticks">
    ///     How long the silence lasts, in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.
    /// </param>
    /// <param name="speechPriority">How much the silence matters: as much as the line it is part of.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are below 0.</exception>
    public void Wait(int ticks, SpeechPriority speechPriority = SpeechPriority.Normal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);

        MakeWayFor(speechPriority);
        _waitingEntries.Enqueue(new SpeechChannelEntry(null, 0f, 0f, ticks));
    }

    /// <summary>Stops the current line and drops every queued line and silence.</summary>
    public void Stop()
    {
        _waitingEntries.Clear();
        _silenceTicksLeft = 0;
        _speechPriority = SpeechPriority.Normal;
        _speechVoice.Stop();
    }

    /// <summary>
    ///     Clears the way for something that matters more than what is being said: stops the line being spoken, ends any
    ///     silence and drops what is waiting. Then remembers how much what is now being said matters.
    /// </summary>
    /// <param name="speechPriority">How much the new line or silence matters.</param>
    private void MakeWayFor(SpeechPriority speechPriority)
    {
        if (IsBusy() && speechPriority > _speechPriority)
        {
            _waitingEntries.Clear();
            _silenceTicksLeft = 0;
            _speechVoice.Stop();
        }

        if (!IsBusy()) _speechPriority = speechPriority;
    }
}
