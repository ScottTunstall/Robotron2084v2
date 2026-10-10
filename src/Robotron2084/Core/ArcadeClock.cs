namespace Robotron2084.Core;

/// <summary>The arcade's frame clock expressed in whole numbers (notes §52).</summary>
/// <remarks>
/// <para>
/// A fiftieth of a second is 6/5 of a port tick, so both are whole numbers of a CLOCK UNIT: a port tick
/// is <see cref="UnitsPerPortTick"/> units and a fiftieth of a second is <see cref="UnitsPerRomFrame"/>.
/// A timer adds <see cref="UnitsPerPortTick"/> each tick and fires each time it has gathered a
/// fiftieth of a second's worth, carrying the remainder, so it never drifts.
/// </para>
/// <para>
/// <b>Why a third unit.</b> The arcade board redraws the screen 50 times a second and its game
/// logic is driven straight off that redraw signal (the "vertical blank", or "vblank", interrupt),
/// so the ROM counts its delays in <i>fiftieths of a second</i> (1 fiftieth of a second = 1/50 second). This port (this C# version of the game, not the arcade machine) runs a
/// fixed 60-updates-per-second game loop (MonoGame's fixed time step, switched on in <see cref="RobotronGame"/>), so its own timing unit — a <i>port tick</i> — is 1/60
/// second. Since 60/50 reduces to 6/5, one fiftieth of a second of arcade time is exactly 6/5 = 1.2 port
/// ticks, and 1.2 is not a whole number: a timer cannot simply count down 4.8 ticks. Floating
/// point would work but drifts out of sync over a long session, so a timer counts in clock units
/// instead — the smallest unit for which both a port tick (5 units) and a fiftieth of a second (6 units) are
/// whole. Each tick it adds 5; when it has gathered <c>romFrames * 6</c> units the timed action
/// fires and that many units are subtracted, carrying any remainder forward. That holds the
/// long-run average exactly on the arcade's clock, with no drift and no floating point. See notes
/// §52 for the derivation and the bug (entities running 17-20% fast) that this pattern replaced.
/// </para>
/// </remarks>
public static class ArcadeClock
{
    /// <summary>Clock units in one port tick (1/60 s).</summary>
    public const int UnitsPerPortTick = 5;

    /// <summary>Clock units in one fiftieth of a second (1/50 s).</summary>
    public const int UnitsPerRomFrame = 6;

    /// <summary>
    /// Converts a delay in fiftieths of a second to the equivalent number of port ticks, so every timer in
    /// this port waits for the same real-world length of time the arcade did.
    /// </summary>
    /// <remarks>Truncates, so it is one tick early for a period that does not divide evenly — see
    /// <see cref="ArcadeClock"/> for the clock itself and <see cref="ToPortTicksRoundedUp"/> for the
    /// first tick a period actually fires on.</remarks>
    public static int ToPortTicks(int romFrames) => romFrames * ArcadeClock.UnitsPerRomFrame / ArcadeClock.UnitsPerPortTick;

    /// <summary>
    /// The FIRST port tick on which a <paramref name="romFrames"/>-period clock running on the
    /// clock units described on <see cref="ArcadeClock"/> fires:
    /// <c>ceil(romFrames * 6 / 5)</c>. Use this in tests that tick to a boundary —
    /// <see cref="ToPortTicks"/> TRUNCATES, so it is always one tick too early for a short
    /// period (<c>ToPortTicks(3)</c> = 3, but the step actually lands on tick 4).
    /// </summary>
    public static int ToPortTicksRoundedUp(int romFrames) => (romFrames * ArcadeClock.UnitsPerRomFrame + 4) / ArcadeClock.UnitsPerPortTick;

    /// <summary>A number of fiftieths of a second, in clock units.</summary>
    public static int ToClockUnits(int romFrames) => romFrames * UnitsPerRomFrame;
}
