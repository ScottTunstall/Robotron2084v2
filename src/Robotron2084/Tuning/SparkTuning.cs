namespace Robotron2084.Tuning;

/// <summary>The spark's ballistic movement and lifetime, and the most sparks allowed in play.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRC11.ASM</c> <c>ENFSHT</c> (makes a spark) and <c>SPARK</c> (runs it each beat)</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>CREATE_SPARK</c> (<c>$1404</c>) and <c>ANIMATE_SPARK</c>
///             (<c>$14A8</c>)
///         </item>
///     </list>
///     A spark is a ballistic particle, not a random walk. <c>ENFSHT</c> sets its starting speed to four times the
///     gap between the spark and a spot near the player, in subpixels, and gives it a constant acceleration on each
///     axis. On every beat <c>SPARK</c> adds the acceleration to the speed, so the path is a parabola. That is the
///     arcade spark's habit of going off course, and it is why a spark can start out moving away from the player.
///     <para>
///         The arcade's object mover (<c>RRS22.ASM</c> <c>OPB80</c>; in the disassembly the unlabelled copy at $DCFF and <c>MOVE_ALL_SPHEROIDS_ENFORCERS_SPARKS_AND_DRAW_IF_BELOW_RASTER_BEAM</c> at $DD90) adds
///         the whole 16-bit speed to the 16-bit position once per frame. A speed of four times the gap therefore
///         covers a sixty-fourth of the gap per frame. A move that would leave the playfield is rejected, not clamped:
///         the spark keeps its last valid coordinate on that axis while the other axis still moves.
///     </para>
///     <para>
///         The port counts in port pixels, so one arcade column is
///         <see cref="Robotron2084.Core.ScreenSize.ToPortPixelsFromColumns" /> port pixels. Speed and acceleration
///         are both held in <see cref="Robotron2084.Core.ScreenSize.SubpixelsPerPixel">subpixels</see>.
///     </para>
/// </remarks>
public static class SparkTuning
{
    /// <summary>How many arcade frames pass between one beat of a spark and the next.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>SPARK</c>, <c>NAP 4</c>. Disassembly: <c>ANIMATE_SPARK</c>
    ///     (<c>$14A8</c>), <c>LDA #$04</c> at <c>$14CC</c>.
    /// </remarks>
    public const int BeatIntervalArcadeFrames = 4;

    /// <summary>The most sparks that may be on the screen at once.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>. Disassembly: <c>CREATE_SPARK</c> (<c>$1404</c>),
    ///     <c>CMPA #$14</c> at <c>$1412</c>.
    /// </remarks>
    public const int MaxActiveSparks = 20;

    /// <summary>
    ///     The biggest change a spark's speed can get on each beat, on either axis: from minus this to just under plus
    ///     this, in the arcade's own speed units.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, <c>ANDB #$1F / ADDB #-$10</c> for <c>PD2</c> and
    ///     <c>PD4</c>. Disassembly: <c>CREATE_SPARK</c> (<c>$1404</c>), <c>$1466</c> and <c>$146F</c>.
    /// </remarks>
    public const int SparkAccelArcadeRange = 16;

    /// <summary>
    ///     How many moves a spark would take to reach the spot it is aimed at, if its speed never changed.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, where the speed is four times the gap in subpixels, and
    ///     <c>RRS22.ASM</c> <c>OPB80</c>, which adds that speed to the position once per frame. Disassembly:
    ///     <c>CREATE_SPARK</c> (<c>$1404</c>), the two <c>ASLB / ROLA</c> pairs at <c>$1445</c> and <c>$1458</c>.
    /// </remarks>
    public const int SparkAimDivisor = 64;

    /// <summary>The widest aim wobble: from minus this to just under plus this, in columns sideways and rows up and down.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, <c>ANDB #$1F / ADDB #-$10</c> on both axes. Disassembly:
    ///     <c>CREATE_SPARK</c> (<c>$1404</c>).
    /// </remarks>
    public const int SparkJitterRange = 16;

    /// <summary>
    ///     How close to the left wall the player has to be, in columns, for the sideways aim wobble to be switched off.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, <c>CMPA #XMIN+$10 / BHS ENFS2 / CLRB</c>. Disassembly:
    ///     <c>CREATE_SPARK</c> (<c>$1404</c>), <c>CMPA #$17 / BCC</c> at <c>$1439</c>.
    /// </remarks>
    public const int SparkLeftWallJitterColumns = 16;

    /// <summary>The longest a spark lasts, in arcade frames.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, <c>PD7</c> = <c>(HSEED &amp; $F) + $14</c> beats of life.
    ///     The port keeps the life in arcade frames, so this is the most beats times
    ///     <see cref="BeatIntervalArcadeFrames" />. Disassembly: <c>CREATE_SPARK</c> (<c>$1404</c>),
    ///     <c>ANDA #$0F / ADDA #$14</c> at <c>$1475</c>.
    /// </remarks>
    public const int SparkLifeMaxArcadeFrames = 140;

    /// <summary>The shortest a spark lasts, in arcade frames.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, <c>PD7</c> = <c>(HSEED &amp; $F) + $14</c> beats of life.
    ///     The port keeps the life in arcade frames, so this is the fewest beats times
    ///     <see cref="BeatIntervalArcadeFrames" />. Disassembly: <c>CREATE_SPARK</c> (<c>$1404</c>),
    ///     <c>ADDA #$14</c> at <c>$1477</c>.
    /// </remarks>
    public const int SparkLifeMinArcadeFrames = 80;

    /// <summary>The fastest a spark may go on either axis, in port pixels for each move.</summary>
    /// <remarks>
    ///     Port only: the arcade has no such limit. It is a safety cap that keeps a spark from jumping a long way in a
    ///     single move.
    /// </remarks>
    public const int SparkMaxSpeed = 8;
}
