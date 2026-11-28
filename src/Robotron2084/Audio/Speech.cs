using Robotron2084.Audio.Models;

namespace Robotron2084.Audio;

/// <summary>
///     The game's speech service: set up once when the game loads, moved on once a port tick, and asked for lines from
///     anywhere in the game.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this is not in the arcade game.</item>
///         <item>
///             It is separate from <see cref="Sound" />. Speech is never cut off by a sound effect, and a line is never
///             cut off by the next line. Sound effects cut each other off, as on the arcade's sound board.
///         </item>
///         <item>
///             Until <see cref="Initialise" /> runs (in the tests, or when the computer has no sound output), asking for
///             a line does nothing.
///         </item>
///     </list>
/// </remarks>
public static class Speech
{
    private static SpeechChannel? _speechChannel;
    private static ISpeechVoice? _speechVoice;

    /// <summary>
    ///     Set by the app shell every tick, with <see cref="Sound.AttractMuted" />: true while the attract sequence is on
    ///     screen and the ATTRACT MODE SOUND setting is off. While it is set, asking for a line does nothing, and no line
    ///     already waiting is started. A real game keeps its speech.
    /// </summary>
    public static bool AttractMuted { get; set; }

    /// <summary>Sets up the speech service with the voice that will speak the lines.</summary>
    /// <param name="speechVoice">The voice.</param>
    public static void Initialise(ISpeechVoice speechVoice)
    {
        Shutdown();
        _speechVoice = speechVoice;
        _speechChannel = new SpeechChannel(speechVoice);
    }

    /// <summary>Gets whether a line is being spoken or waiting to be spoken.</summary>
    public static bool IsBusy()
    {
        return _speechChannel?.IsBusy() ?? false;
    }

    /// <summary>Asks for a line to be spoken after any lines already waiting.</summary>
    /// <param name="lineName">The name of the line: the WAV file's name without the extension.</param>
    public static void Say(string lineName)
    {
        if (AttractMuted) return;

        _speechChannel?.Say(lineName);
    }

    /// <summary>
    ///     Asks for the Berzerk robot to say words, one after another, at the pitch and volume its samples were recorded at
    ///     (<see cref="BerzerkVoiceSetting.Sample" />), after anything already waiting.
    /// </summary>
    /// <param name="berzerkWords">The words, in order, such as The, Humanoid, Must, Not, Escape. A Pause is a short silence.</param>
    public static void SayBerzerk(params BerzerkWord[] berzerkWords)
    {
        SayBerzerk(BerzerkVoiceSetting.Sample, berzerkWords);
    }

    /// <summary>
    ///     Asks for the Berzerk robot to say words, one after another, at one of its pitches and volumes, after anything
    ///     already waiting.
    /// </summary>
    /// <param name="berzerkVoiceSetting">
    ///     The pitch, where higher is higher and quicker, and the volume, each from 0 to 7.
    /// </param>
    /// <param name="berzerkWords">The words, in order, such as The, Humanoid, Must, Not, Escape. A Pause is a short silence.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pitch or the volume is not from 0 to 7.</exception>
    public static void SayBerzerk(BerzerkVoiceSetting berzerkVoiceSetting, params BerzerkWord[] berzerkWords)
    {
        SayBerzerk(berzerkVoiceSetting, SpeechPriority.Normal, berzerkWords);
    }

    /// <summary>
    ///     Asks for the Berzerk robot to say words, one after another, at one of its pitches and volumes. They wait behind
    ///     anything already waiting, unless they matter more than what is being said: then that is stopped and dropped.
    /// </summary>
    /// <param name="berzerkVoiceSetting">The pitch, where higher is higher and quicker, and the volume, each from 0 to 7.</param>
    /// <param name="speechPriority">How much the words matter.</param>
    /// <param name="berzerkWords">The words, in order. A Pause is a short silence.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pitch or the volume is not from 0 to 7.</exception>
    public static void SayBerzerk(BerzerkVoiceSetting berzerkVoiceSetting, SpeechPriority speechPriority, params BerzerkWord[] berzerkWords)
    {
        var pitchOctaves = BerzerkVoice.GetPitchOctaves(berzerkVoiceSetting.Pitch);
        var volumeShare = BerzerkVoice.GetVolumeShare(berzerkVoiceSetting.Volume);
        if (AttractMuted || _speechChannel is not { } speechChannel) return;

        foreach (var berzerkWord in berzerkWords)
        {
            if (berzerkWord == BerzerkWord.Pause)
            {
                speechChannel.Wait(BerzerkVoice.PauseTicks, speechPriority);
                continue;
            }

            speechChannel.Say(BerzerkVoice.GetLineName(berzerkWord), pitchOctaves, volumeShare, speechPriority);
        }
    }

    /// <summary>Asks for Gorf to say a line, raised or lowered if asked, after anything already waiting.</summary>
    /// <param name="gorfLine">The line.</param>
    /// <param name="pitchOctaves">
    ///     How far to raise (more than 0) or lower (less than 0) the line, in octaves, up to
    ///     <see cref="GorfVoice.MaximumPitchOctaves" /> either way. 0, unless given, says it as recorded. Raised, it is
    ///     also quicker; lowered, slower.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The pitch is more than an octave either way.</exception>
    public static void SayGorf(GorfLine gorfLine, float pitchOctaves = 0f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pitchOctaves, -GorfVoice.MaximumPitchOctaves);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pitchOctaves, GorfVoice.MaximumPitchOctaves);
        if (AttractMuted) return;

        _speechChannel?.Say(GorfVoice.GetLineName(gorfLine), pitchOctaves);
    }

    /// <summary>Stops Berzerk or Gorf speech and drops anything waiting to be spoken.</summary>
    public static void Stop()
    {
        _speechChannel?.Stop();
    }

    /// <summary>
    ///     Switches the speech service off and frees what the voice holds, if it holds anything that needs freeing. Asking
    ///     for a line afterwards does nothing, until <see cref="Initialise" /> runs again.
    /// </summary>
    public static void Shutdown()
    {
        (_speechVoice as IDisposable)?.Dispose();
        _speechVoice = null;
        _speechChannel = null;
    }

    /// <summary>Moves the speech on by one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    public static void Tick()
    {
        if (AttractMuted) return;

        _speechChannel?.Tick();
    }

    /// <summary>Puts the speech service back as it is before <see cref="Initialise" />: no voice, and nothing muted (test hook).</summary>
    internal static void ResetForTesting()
    {
        _speechChannel = null;
        _speechVoice = null;
        AttractMuted = false;
    }
}
