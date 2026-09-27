namespace Robotron2084.Audio;

/// <summary>
/// The game's one sound service. Like the arcade, which has one sound board playing one sound at a
/// time, the port has one <see cref="SoundEngine"/>: set up once when the game loads, moved on once a
/// port tick, and asked for sounds from anywhere in the game.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRS22.ASM</c>, routine <c>SNDLDV</c> (reached through the <c>SNDLD</c>
/// jump vector in <c>RRF.ASM</c>).</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$D3C7</c>.</item>
/// </list>
/// Until <see cref="Initialize"/> runs (in the tests, or when the sound ROM is missing), asking for a
/// sound does nothing.
/// </remarks>
public static class Sound
{
    private static TransporterSound _transporter = new();
    private static SoundEngine? _engine;

    /// <summary>
    /// The master switch: on unless the game is started with the environment variable
    /// <c>ROBOTRON2084_SOUND=0</c>. While it is off, asking for a sound does nothing.
    /// </summary>
    public static bool Enabled { get; set; } =
        Environment.GetEnvironmentVariable("ROBOTRON2084_SOUND") != "0";

    /// <summary>Sets up the sound service with the sink that will play the sound numbers.</summary>
    /// <param name="sink">Where the sound numbers go.</param>
    public static void Initialize(IAudioSink sink)
    {
        _engine = new SoundEngine(sink);
        _transporter = new TransporterSound();
    }

    /// <summary>Asks for a sound that is heard in the middle, between both speakers.</summary>
    /// <param name="sequence">The sound's table.</param>
    public static void Play(SoundSequence sequence) => Play(sequence, 0f);

    /// <summary>Asks for a sound that is heard from a place between the speakers.</summary>
    /// <param name="sequence">The sound's table.</param>
    /// <param name="pan">Where the sound is heard: -1 is wholly left, 0 the middle, 1 wholly right.</param>
    public static void Play(SoundSequence sequence, float pan)
    {
        if (!Enabled)
        {
            return;
        }

        _engine?.Play(sequence, pan);
    }

    /// <summary>Starts the transporter's warp-in hum, as a brain wave's robots are beamed in.</summary>
    public static void PlayTransporter()
    {
        if (!Enabled || _engine is null)
        {
            return;
        }

        _transporter.Start(_engine);
    }

    /// <summary>Moves the sound on by one port tick.</summary>
    public static void Tick()
    {
        if (!Enabled || _engine is null)
        {
            return;
        }

        _engine.Tick();
        _transporter.Tick(_engine);
    }
}
