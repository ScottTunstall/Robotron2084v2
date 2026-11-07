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

    /// <summary>Sets up the speech service with the voice that will speak the lines.</summary>
    /// <param name="speechVoice">The voice.</param>
    public static void Initialise(ISpeechVoice speechVoice)
    {
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
        _speechChannel?.Say(lineName);
    }

    /// <summary>Moves the speech on by one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    public static void Tick()
    {
        _speechChannel?.Tick();
    }
}
