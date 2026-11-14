namespace Robotron2084.Core;

/// <summary>The units the game measures time in, and how to change one into another.</summary>
public static class ArcadeClock
{
    /// <summary>How many FPS the original arcade game ran at.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             The arcade redrew its screen this many times a second, and ran its game code once for each redraw.
    ///         </item>
    ///         <item>
    ///             So the arcade measured every wait in these. Names in the code call one an arcade frame
    ///             (<c>...ArcadeFrames</c>), and the arcade's source calls the redraw a vblank.
    ///         </item>
    ///     </list>
    /// </remarks>
    public const int ArcadeFramesPerSecond = 50;

    /// <summary>How many clock units there are in a second.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             A clock unit is a abstraction to make translating arcade game time into this port game time easier. Both a tick (<see cref="PortTicksPerSecond" />) and a fiftieth
    ///             of a second (<see cref="ArcadeFramesPerSecond" />) are a whole number of clock units.
    ///         </item>
    ///         <item>
    ///             That lets timers count both in <c>int</c>s, so nothing is lost to rounding. This is the smallest
    ///             number that both <see cref="PortTicksPerSecond" /> and <see cref="ArcadeFramesPerSecond" /> divide into.
    ///         </item>
    ///     </list>
    /// </remarks>
    public const int ClockUnitsPerSecond = 300;

    /// <summary>
    ///     How often the arcade moves everything that is moving, in fiftieths of a second
    ///     (<see cref="ArcadeFramesPerSecond" />).
    /// </summary>
    /// <remarks>ROM: <c>RRS22.ASM</c> <c>OPB80</c>, which moves every moving object once each frame.</remarks>
    public const int MoveIntervalArcadeFrames = 1;

    /// <summary>How many ticks the game has each second.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             A tick is one call of <see cref="Robotron2084.RobotronGame.Update(Microsoft.Xna.Framework.GameTime)" />.
    ///         </item>
    ///         <item>The game's code only runs during a tick, so nothing can happen between two ticks.</item>
    ///         <item><see cref="Robotron2084.RobotronGame" /> sets MonoGame's fixed time step from this.</item>
    ///     </list>
    /// </remarks>
    public const int PortTicksPerSecond = 60;

    /// <summary>How many clock units (<see cref="ClockUnitsPerSecond" />) there are in one fiftieth of a second.</summary>
    public const int UnitsPerArcadeFrame = ClockUnitsPerSecond / ArcadeFramesPerSecond;

    /// <summary>How many clock units (<see cref="ClockUnitsPerSecond" />) there are in one tick.</summary>
    public const int UnitsPerPortTick = ClockUnitsPerSecond / PortTicksPerSecond;

    /// <summary>
    ///     Changes fiftieths of a second (<see cref="ArcadeFramesPerSecond" />) into clock units
    ///     (<see cref="ClockUnitsPerSecond" />).
    /// </summary>
    /// <param name="arcadeFrames">The time, in fiftieths of a second.</param>
    /// <returns>The same time, in clock units.</returns>
    public static int ToClockUnits(int arcadeFrames)
    {
        return arcadeFrames * UnitsPerArcadeFrame;
    }

    /// <summary>
    ///     Changes fiftieths of a second (<see cref="ArcadeFramesPerSecond" />) into ticks
    ///     (<see cref="PortTicksPerSecond" />), rounding down.
    /// </summary>
    /// <param name="arcadeFrames">The time, in fiftieths of a second.</param>
    /// <returns>The time in ticks, rounded down to a whole tick.</returns>
    /// <remarks>
    ///     Rounding down means it can be a tick shorter than a <see cref="ArcadeFrameTimer" /> with the same wait. Use
    ///     <see cref="ToPortTicksRoundedUp" /> to find the tick a <see cref="ArcadeFrameTimer" /> first says "over" on.
    /// </remarks>
    public static int ToPortTicks(int arcadeFrames)
    {
        return ToClockUnits(arcadeFrames) / UnitsPerPortTick;
    }

    /// <summary>
    ///     Changes fiftieths of a second (<see cref="ArcadeFramesPerSecond" />) into ticks
    ///     (<see cref="PortTicksPerSecond" />), rounding up.
    /// </summary>
    /// <param name="arcadeFrames">The time, in fiftieths of a second.</param>
    /// <returns>The time in ticks, rounded up to a whole tick.</returns>
    /// <remarks>
    ///     This is the tick on which a new <see cref="ArcadeFrameTimer" /> first says this wait is over. Tests use it to
    ///     tick to that moment.
    /// </remarks>
    public static int ToPortTicksRoundedUp(int arcadeFrames)
    {
        return (ToClockUnits(arcadeFrames) + UnitsPerPortTick - 1) / UnitsPerPortTick;
    }
}
