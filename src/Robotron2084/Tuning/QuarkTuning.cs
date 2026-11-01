namespace Robotron2084.Tuning;

/// <summary>How fast a quark drifts, how far it keeps from the walls, how it leaves the screen, and how it is drawn.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRTK4.ASM</c> <c>SQUARE</c> and <c>SQVEL</c> (notes §43 and §51)</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>CHANGE_QUARK_DIRECTION</c> (<c>$4B82</c>) and <c>ANIMATE_QUARK</c>
///             (<c>$4BFB</c>)
///         </item>
///     </list>
///     The quark drifts. It does not seek waypoints, and its speed is not proportional to any distance. <c>SQVEL</c>
///     sets its sideways speed to a random amount from 1 to <c>SQSPD</c>, times 4, and its up-and-down speed to a
///     random amount from 1 to <c>SQSPD</c>, times 8, both in 256ths of a pixel for each frame, so a unit of speed
///     goes twice as far up and down as sideways. <c>SQSPD</c> comes from the wave table. Near a wall the sign is
///     forced away from the wall first, and only otherwise is it taken from a bit of the random seed (on the X axis a
///     set bit means negative, on the Y axis it means positive, so the two axes are not tied together).
/// </remarks>
public static class QuarkTuning
{
    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of a quark and the next.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>SQUARE</c>, <c>NAP 3</c>. A <c>NAP n</c> waits one more frame than its
    ///     number. Disassembly: <c>ANIMATE_QUARK</c> (<c>$4BFB</c>), <c>LDA #$03</c> at <c>$4C1C</c>.
    /// </remarks>
    public const int BeatIntervalArcadeFrames = 4;

    /// <summary>How far above the bottom edge, in rows, a fleeing quark is counted as gone.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQ3L</c>, <c>YMAX-16</c>.</remarks>
    public const int FleeExitHighRows = 16;

    /// <summary>How far below the top edge, in rows, a fleeing quark is counted as gone.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQ3L</c>, <c>YMIN+2</c>.</remarks>
    public const int FleeExitLowRows = 2;

    /// <summary>How fast a fleeing quark goes up or down, in 256ths of a row for each frame.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>SQ3</c>, where the sideways speed is zero and the up-and-down speed is
    ///     plus or minus <c>$0200</c>.
    /// </remarks>
    public const int FleeVelocityRom = 0x0200;

    /// <summary>The most beats a quark can go on before it picks a new direction.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>PD7</c> = <c>(SEED &amp; $1F) + 1</c>. Disassembly:
    ///     <c>CHANGE_QUARK_DIRECTION</c> (<c>$4B82</c>), <c>ANDA #$1F / INCA</c> at <c>$4BC3</c>.
    /// </remarks>
    public const int NewDirectionMaxBeats = 32;

    /// <summary>How many animation frames a quark has once it has started dropping tanks.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQP0</c> to <c>SQP8</c>.</remarks>
    public const int TotalAnimationFrames = 9;

    /// <summary>How many animation frames a quark has while it wanders.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQP0</c> to <c>SQP4</c>.</remarks>
    public const int TravelAnimationFrames = 5;

    /// <summary>The number the random sideways speed is multiplied by.</summary>
    /// <remarks>
    ///     Two <c>ASLB / ROLA</c> pairs. Disassembly: <c>CHANGE_QUARK_DIRECTION</c> (<c>$4B82</c>), <c>$4B9A</c> to
    ///     <c>$4B9D</c>.
    /// </remarks>
    public const int VelocityXScale = 4;

    /// <summary>The number the random up-and-down speed is multiplied by.</summary>
    /// <remarks>
    ///     Three <c>ASLB / ROLA</c> pairs. Disassembly: <c>CHANGE_QUARK_DIRECTION</c> (<c>$4B82</c>), <c>$4BB8</c> to
    ///     <c>$4BBD</c>.
    /// </remarks>
    public const int VelocityYScale = 8;

    /// <summary>How close to the bottom edge, in rows, a quark must be before it is turned back up.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>YMAX-20</c>.</remarks>
    public const int WallMarginBottomRows = 20;

    /// <summary>How close to the left edge, in columns, a quark must be before it is turned back right.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>XMIN+5</c> (columns, so 10 arcade pixels).</remarks>
    public const int WallMarginLeftColumns = 5;

    /// <summary>How close to the right edge, in columns, a quark must be before it is turned back left.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>XMAX-12</c> (columns, so 24 arcade pixels).</remarks>
    public const int WallMarginRightColumns = 12;

    /// <summary>How close to the top edge, in rows, a quark must be before it is turned back down.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>YMIN+5</c>.</remarks>
    public const int WallMarginTopRows = 5;
}
