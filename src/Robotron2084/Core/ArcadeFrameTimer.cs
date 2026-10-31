namespace Robotron2084.Core;

/// <summary>
///     Tells its owner when a wait, measured in <see cref="ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see>,
///     is over.
/// </summary>
/// <remarks>
///     <para>Why it is needed:</para>
///     <list type="bullet">
///         <item>
///             The arcade game measured its waits in <see cref="ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see>,
///             but the game can only act on a <see cref="ArcadeClock.PortTicksPerSecond">tick</see>.
///         </item>
///         <item>
///             The two are not the same length, so most waits do not end exactly on a
///             <see cref="ArcadeClock.PortTicksPerSecond">tick</see>.
///         </item>
///         <item>
///             If the owner just counted whole <see cref="ArcadeClock.PortTicksPerSecond">ticks</see>, each wait
///             would end a little early or a little late, and over many waits the owner would drift out of step with
///             the arcade.
///         </item>
///     </list>
///     <para>What it does:</para>
///     <list type="bullet">
///         <item>
///             It says the wait is over on the first <see cref="ArcadeClock.PortTicksPerSecond">tick</see> at or
///             after the moment the wait really ends.
///         </item>
///         <item>
///             It keeps the time left over past that moment, and counts it towards the next wait. So the owner never
///             drifts out of step.
///         </item>
///     </list>
///     <para>How to use it:</para>
///     <list type="bullet">
///         <item>
///             Call <see cref="Tick" /> once each time the owner's
///             <see cref="Robotron2084.Entities.IEntity.Update(Microsoft.Xna.Framework.GameTime, Robotron2084.Level.PlayField)" />
///             runs.
///         </item>
///         <item>
///             Pass the wait each time. The timer does not store it, so the owner may pass a different wait on each
///             call.
///         </item>
///         <item>When <see cref="Tick" /> returns true, the wait is over.</item>
///     </list>
///     <para>A beat:</para>
///     <list type="bullet">
///         <item>A beat is an entity's turn to move or act.</item>
///         <item>
///             An entity that has beats keeps a <see cref="ArcadeFrameTimer" /> for them, and passes it the time between
///             beats, in <see cref="ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see>.
///         </item>
///     </list>
///     <code>if (_beatTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(field);</code>
/// </remarks>
public sealed class ArcadeFrameTimer
{
    /// <summary>
    ///     The time gathered towards the current wait, in <see cref="ArcadeClock.ClockUnitsPerSecond">clock units</see>.
    /// </summary>
    private int _clockUnits;

    /// <summary>Makes a timer with no time gathered.</summary>
    public ArcadeFrameTimer()
    {
    }

    /// <summary>Makes a timer that starts with some time already gathered.</summary>
    /// <param name="clockUnits">
    ///     The time already gathered, in <see cref="ArcadeClock.ClockUnitsPerSecond">clock units</see>. A negative
    ///     value makes the first wait longer.
    /// </param>
    /// <remarks>Use it when the timer must keep in step with another clock that is already running.</remarks>
    public ArcadeFrameTimer(int clockUnits)
    {
        _clockUnits = clockUnits;
    }

    /// <summary>
    ///     Makes a timer that has already gathered the whole of <paramref name="intervalArcadeFrames" />, so the first call
    ///     of <see cref="Tick" /> with that same wait returns true.
    /// </summary>
    /// <param name="intervalArcadeFrames">
    ///     The wait the owner will pass to <see cref="Tick" />, in
    ///     <see cref="ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see>.
    /// </param>
    /// <remarks>
    ///     Use it for something that must happen on the owner's very first
    ///     <see cref="ArcadeClock.PortTicksPerSecond">tick</see>.
    /// </remarks>
    public static ArcadeFrameTimer CreateDue(int intervalArcadeFrames)
    {
        return new ArcadeFrameTimer(ArcadeClock.ToClockUnits(intervalArcadeFrames));
    }

    /// <summary>Throws away all the time gathered, so the next wait is counted in full.</summary>
    /// <remarks>Use it when the owner starts something again, so time left over from before cannot cut it short.</remarks>
    public void Restart()
    {
        _clockUnits = 0;
    }

    /// <summary>
    ///     Counts one <see cref="ArcadeClock.PortTicksPerSecond">tick</see>, and says whether the wait is over.
    /// </summary>
    /// <param name="intervalArcadeFrames">
    ///     How long the wait is, in <see cref="ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see>. It may be
    ///     different from the wait passed last time.
    /// </param>
    /// <returns>True if the wait is over on this <see cref="ArcadeClock.PortTicksPerSecond">tick</see>.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             It adds one <see cref="ArcadeClock.PortTicksPerSecond">tick</see>
    ///             (<see cref="ArcadeClock.UnitsPerPortTick" />) to the time gathered.
    ///         </item>
    ///         <item>
    ///             If the time gathered is now at least the wait, it takes the wait off, keeps whatever is left for the
    ///             next wait, and returns true.
    ///         </item>
    ///         <item>Otherwise it returns false.</item>
    ///         <item>
    ///             It can only say "over" once per call. So a wait of zero or less returns true on every call.
    ///         </item>
    ///     </list>
    /// </remarks>
    public bool Tick(int intervalArcadeFrames)
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        var intervalClockUnits = ArcadeClock.ToClockUnits(intervalArcadeFrames);
        if (_clockUnits < intervalClockUnits) return false;

        _clockUnits -= intervalClockUnits;
        return true;
    }
}
