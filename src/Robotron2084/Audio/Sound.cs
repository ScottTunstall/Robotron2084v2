namespace Robotron2084.Audio;

/// <summary>
/// Process-wide sound service. The arcade has exactly ONE sound board (one
/// voice, PIA port B), so the port mirrors that with a single
/// <see cref="SoundEngine"/> — created once in
/// <c>RobotronGame.LoadContent</c> (<see cref="Initialize"/>), ticked once
/// per port tick in <c>RobotronGame.Update</c>, and requested from anywhere
/// in the field code via <see cref="Play"/>. Uninitialised (unit tests,
/// no-audio contexts) <see cref="Play"/> is a no-op.
/// </summary>
public static class Sound
{
    private static SoundEngine? _engine;

    /// <summary>
    /// MASTER SWITCH for the port's audio — **OFF by default**. The only sink the port has is the STUB
    /// square-wave beeper (notes §36.2): the real note→frequency map is sound-board hardware, not in the CPU
    /// ROM, so what you hear is deliberately NOT the arcade, and there is nothing worth hearing until that
    /// table lands.
    /// <para>
    /// To hear it: set this to <c>true</c>, or start the game with the environment
    /// variable <c>ROBOTRON2084_SOUND=1</c> (no rebuild needed). The sequencer,
    /// its tests and the ROM sound tables are unaffected either way — only the
    /// sink's beeping is switched off.
    /// </para>
    /// </summary>
    public static bool Enabled { get; set; } =
        Environment.GetEnvironmentVariable("ROBOTRON2084_SOUND") == "1";

    /// <summary>True once <see cref="Initialize"/> has run (audio active).</summary>
    public static bool IsReady => _engine is not null;

    public static void Initialize(IAudioSink sink) => _engine = new SoundEngine(sink);

    /// <summary>
    /// One port tick (≈ one arcade vblank). Drives the sequencer + sink.
    /// </summary>
    public static void Tick()
    {
        if (!Enabled)
        {
            return;
        }

        _engine?.Tick();
    }

    /// <summary>
    /// Request a ROM sound (priority preemption per the ROM, notes §36.2).
    /// A no-op while <see cref="Enabled"/> is false or before
    /// <see cref="Initialize"/> (unit tests, no-audio contexts).
    /// </summary>
    public static void Play(SoundSequence sequence)
    {
        if (!Enabled)
        {
            return;
        }

        _engine?.Play(sequence);
    }
}
