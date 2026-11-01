namespace Robotron2084.Tuning;

/// <summary>How fast an enforcer grows when it is dropped, and how often it makes a decision.</summary>
public static class EnforcerTuning
{
    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of an enforcer and the next.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>ENFR1B</c>, <c>NAP 3</c></item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ENFORCER_AI</c> (<c>$139D</c>), <c>LDA #$03</c> at <c>$13AD</c></item>
    ///     </list>
    ///     A <c>NAP n</c> waits one more frame than its number, so this is one more than the number in the instruction.
    ///     The enforcer's timer for choosing a new place to go and its timer for firing a spark both count these beats.
    /// </remarks>
    public const int BeatIntervalArcadeFrames = 4;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> each animation frame of
    ///     the enforcer's growing shows for.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>ENFRCE</c>, <c>NAP 8</c> for each spawn animation frame</item>
    ///         <item>
    ///             Disassembly: <c>asm/robomame.asm</c> <c>ENFORCER_SPAWN_ANIMATION</c> (<c>$1381</c>), <c>LDA #$08</c> at
    ///             <c>$1390</c>
    ///         </item>
    ///     </list>
    ///     A <c>NAP n</c> waits one more frame than its number, as it does for the quark.
    /// </remarks>
    public const int GrowIntervalArcadeFrames = 9;

    /// <summary>How many animation frames the enforcer shows while it grows.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>ENFR0</c></item>
    ///         <item>
    ///             Disassembly: <c>asm/robomame.asm</c> <c>ENFORCER_SPAWN_ANIMATION</c> (<c>$1381</c>), the check for the
    ///             last spawn animation frame at <c>$1388</c>
    ///         </item>
    ///     </list>
    /// </remarks>
    public const int GrowSteps = 5;
}
