namespace Robotron2084.Audio;

/// <summary>
///     Keeps the lines waiting to be spoken, and gives the voice the next one when it has finished the last. A line is
///     never cut off.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this is not in the arcade game.</item>
///         <item>
///             It does not touch the sound board. Sound effects on the board cut each other off, as in the arcade, and
///             never cut off the speech.
///         </item>
///         <item>A line asked for while another is being spoken waits its turn, in the order it was asked for.</item>
///         <item>It only knows the voice it was given (<see cref="ISpeechVoice" />).</item>
///     </list>
/// </remarks>
public sealed class SpeechChannel
{
    private readonly Queue<string> _waitingLineNames = new();
    private readonly ISpeechVoice _speechVoice;

    /// <summary>Makes a channel with nothing waiting.</summary>
    /// <param name="speechVoice">The voice that speaks the lines.</param>
    public SpeechChannel(ISpeechVoice speechVoice)
    {
        _speechVoice = speechVoice;
    }

    /// <summary>Gets whether a line is being spoken or waiting to be spoken.</summary>
    public bool IsBusy()
    {
        return _speechVoice.IsSpeaking() || _waitingLineNames.Count > 0;
    }

    /// <summary>Asks for a line to be spoken after any lines already waiting. It never stops the line being spoken.</summary>
    /// <param name="lineName">The name of the line.</param>
    public void Say(string lineName)
    {
        _waitingLineNames.Enqueue(lineName);
    }

    /// <summary>Gives the voice the next waiting line, if the voice has finished the last one.</summary>
    public void Tick()
    {
        var isVoiceFree = !_speechVoice.IsSpeaking();
        var hasWaitingLine = _waitingLineNames.Count > 0;
        if (!isVoiceFree || !hasWaitingLine) return;

        var nextLineName = _waitingLineNames.Dequeue();
        _speechVoice.Speak(nextLineName);
    }
}
