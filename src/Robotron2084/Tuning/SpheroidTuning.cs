namespace Robotron2084.Tuning;

/// <summary>The spheroid's speed limits, the places where it leaves the screen, and where it may first appear.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRC11.ASM</c> <c>CIRCLE</c>, <c>CIRNAC</c> and <c>CIRGO</c> (notes §56)</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AD</c>),
///             <c>UPDATE_SPHEROID_MOVEMENT_DELTAS</c> (<c>$1279</c>)
///         </item>
///     </list>
///     The spheroid has no constant speed. It gathers a random acceleration and damps it by a sixty-fourth on every
///     beat, so its speed comes out of that. The limits below are the ROM's own.
/// </remarks>
public static class SpheroidTuning
{
    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of a spheroid and the next.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>CIRCLE</c>, <c>NAP 2</c>. A <c>NAP n</c> waits one more frame than its
    ///     number. Disassembly: <c>ANIMATE_SPHEROID</c> (<c>$11AD</c>).
    /// </remarks>
    public const int BeatIntervalArcadeFrames = 3;

    /// <summary>The column at the left of the screen where a leaving spheroid disappears.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>CIRC3L</c>, <c>CMPA #XMIN+3</c>, where <c>XMIN</c> is 7
    ///     (<c>RRF.ASM</c> lines 69 and 70). Disassembly: <c>TIME_FOR_SPHEROID_TO_EXIT_PRONTO</c> (<c>$1222</c>),
    ///     <c>CMPA #$0A</c> at <c>$1241</c>.
    /// </remarks>
    public const int EscapeExitLeftColumn = 10;

    /// <summary>The column at the right of the screen where a leaving spheroid disappears.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>CIRC3L</c>, <c>CMPA #XMAX-10</c>, where <c>XMAX</c> is <c>$8F</c>
    ///     (<c>RRF.ASM</c> lines 69 and 70). Disassembly: <c>TIME_FOR_SPHEROID_TO_EXIT_PRONTO</c> (<c>$1222</c>),
    ///     <c>CMPA #$85</c> at <c>$1245</c>.
    /// </remarks>
    public const int EscapeExitRightColumn = 133;

    /// <summary>The fastest a spheroid may go sideways, in 256ths of a column for each frame.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>, the X speed limit <c>$0100</c>, which is one column for each
    ///     frame. Disassembly: <c>UPDATE_SPHEROID_MOVEMENT_DELTAS</c> (<c>$1279</c>), <c>CMPD #$0100</c> at
    ///     <c>$127E</c>.
    /// </remarks>
    public const int MaxVelocityXSubpixels = 0x0100;

    /// <summary>The fastest a spheroid may go up or down, in 256ths of a row for each frame.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>, the Y speed limit <c>$0200</c>, which is two rows for each
    ///     frame and so the same distance as the sideways limit. Disassembly:
    ///     <c>UPDATE_SPHEROID_MOVEMENT_DELTAS</c> (<c>$1279</c>), <c>CMPD #$0200</c> at <c>$12A5</c>.
    /// </remarks>
    public const int MaxVelocityYSubpixels = 0x0200;

    /// <summary>How far from the player a spheroid must start, in arcade pixels.</summary>
    /// <remarks>The distance comes from the arcade spec, not from a ROM instruction the port has traced.</remarks>
    public const int MinDistanceFromPlayer = 100;

    /// <summary>How close to a wall a spot counts as "near the wall" for the spheroid's start, in arcade pixels.</summary>
    /// <remarks>The distance comes from the arcade spec, not from a ROM instruction the port has traced.</remarks>
    public const int NearWallBiasDistance = 30;

    /// <summary>The share of spheroid start spots that are chosen near a wall, as a percentage.</summary>
    /// <remarks>The share comes from the arcade spec, not from a ROM instruction the port has traced.</remarks>
    public const int NearWallBiasPercent = 70;
}
