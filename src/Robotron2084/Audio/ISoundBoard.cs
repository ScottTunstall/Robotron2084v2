namespace Robotron2084.Audio;

/// <summary>
/// A sound board: it is sent sound numbers and plays each one as a changing output level, which the
/// arcade's amplifier turns into sound. Time is counted in the board's own clock cycles.
/// </summary>
public interface ISoundBoard
{
    /// <summary>The level the board is sending to the loudspeaker circuit right now, 0 to 255.</summary>
    byte OutputLevel { get; }

    /// <summary>
    /// Runs the board on. It stops at or soon after <paramref name="maxCycles"/>, and never before its
    /// output level has had the chance to change.
    /// </summary>
    /// <param name="maxCycles">The most cycles the caller wants run; at least 1.</param>
    /// <returns>How many cycles ran: at least 1, and a few more than asked for at most.</returns>
    int Run(int maxCycles);

    /// <summary>Sends the board a sound number. The sound playing stops at once and the new one starts.</summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    void SendSoundNumber(int soundNumber);

    /// <summary>True while a sound is waiting to start or still running; false once it has played to its end (a sound that never ends stays true).</summary>
    bool IsPlaying => false;
}
