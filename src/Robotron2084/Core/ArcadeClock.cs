namespace Robotron2084.Core;

/// <summary>The arcade's frame clock expressed in whole numbers (notes §52).</summary>
/// <remarks>
/// <para>
/// A ROM frame is 6/5 of a port tick, so both are whole numbers of a CLOCK UNIT: a port tick
/// is <see cref="UnitsPerPortTick"/> units and a ROM frame is <see cref="UnitsPerRomFrame"/>.
/// A timer adds <see cref="UnitsPerPortTick"/> each tick and fires each time it has gathered a
/// ROM frame's worth, carrying the remainder, so it never drifts.
/// </para>
/// <para>
/// <b>Why a third unit.</b> The arcade board redraws the screen 50 times a second and its game
/// logic is driven straight off that redraw signal (the "vertical blank", or "vblank", interrupt),
/// so the ROM counts its delays in <i>ROM frames</i> (1 ROM frame = 1/50 second). This port runs a
/// fixed 60-updates-per-second game loop, so its own timing unit — a <i>port tick</i> — is 1/60
/// second. Since 60/50 reduces to 6/5, one ROM frame of arcade time is exactly 6/5 = 1.2 port
/// ticks, and 1.2 is not a whole number: a timer cannot simply count down 4.8 ticks. Floating
/// point would work but drifts out of sync over a long session, so a timer counts in clock units
/// instead — the smallest unit for which both a port tick (5 units) and a ROM frame (6 units) are
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

    /// <summary>Clock units in one ROM frame (1/50 s).</summary>
    public const int UnitsPerRomFrame = 6;

    /// <summary>A number of ROM frames, in clock units.</summary>
    public static int Units(int romFrames) => romFrames * UnitsPerRomFrame;
}
