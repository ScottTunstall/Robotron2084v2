using Robotron2084.Core;

namespace Robotron2084.Level;

/// <summary>
///     Keeps the time at the start of a wave. It says when the player appears, and when the game goes live: the
///     moment the player can move and fire, things can touch the player, and the robots start to act.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>PLS0A</c> to <c>PLS2</c>, and
///             <c>APPEAR</c>
///         </item>
///         <item>
///             Disassembly: <c>$2831</c> to <c>$289A</c>, and the appear loop at <c>$28FE</c> to
///             <c>$2962</c>
///         </item>
///     </list>
///     The arcade sets the wave up with everything switched off (
///     <c>
///         LDA #$19
///         / STA STATUS
///     </c>
///     ), brings the robots in, brings the player in (<c>PLS1</c>), and then switches
///     everything on (<c>CLR STATUS</c> at <c>PLS2</c>). The times here are counted in fiftieths of a second from
///     when the wave is set up, on the clock described on <see cref="ArcadeClock" /> (notes §141, §142).</item>
///     </list>
/// </remarks>
public sealed class WaveStartSequence
{
    /// <summary>
    ///     How many passes the arcade's appear loop makes beyond one for each robot. The loop starts one robot's appear
    ///     effect on each pass, and it does not finish until it has made more passes than this and has then walked the whole
    ///     list of robots. It is added to the number of robots to give the number of passes.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, <c>CMPA #32 / BLS AP4</c>.</item>
    ///         <item>Disassembly: <c>$2930</c>.</item>
    ///     </list>
    /// </remarks>
    private const int AppearExtraPasses = 32;

    /// <summary>How many fiftieths of a second the arcade sleeps between one pass of its appear loop and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>NAP 1,APL</c>.</item>
    ///         <item>Disassembly: <c>$2949</c>.</item>
    ///     </list>
    /// </remarks>
    private const int AppearPassRomFrames = 1;

    /// <summary>
    ///     How many fiftieths of a second the arcade sleeps after its appear loop finishes, to let the last appear
    ///     effects finish.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>NAP 2,APX2</c> ("LET EVERYONE FINISH").</item>
    ///         <item>Disassembly: <c>$2953</c>.</item>
    ///     </list>
    /// </remarks>
    private const int AppearFinishNapRomFrames = 2;

    /// <summary>How many fiftieths of a second the arcade then sleeps, with the robots switched on, before the player appears.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>NAP 10,PLS1</c>.</item>
    ///         <item>Disassembly: <c>$295D</c>.</item>
    ///     </list>
    /// </remarks>
    private const int RobotsOnNapRomFrames = 10;

    /// <summary>
    ///     How many fiftieths of a second after the wave is set up the player appears on a brain wave, where the robots
    ///     are beamed in and the appear loop is not run.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>NAP 150,PLS1</c>.</item>
    ///         <item>Disassembly: <c>$2869</c>.</item>
    ///     </list>
    /// </remarks>
    private const int BrainWavePlayerAppearRomFrames = 150;

    /// <summary>
    ///     How many fiftieths of a second the arcade sleeps after the player starts to appear, before the second part of
    ///     the player's appear, which is the strips that lean.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>NAP 06,PLS1A</c>.</item>
    ///         <item>Disassembly: <c>$287A</c>.</item>
    ///     </list>
    /// </remarks>
    private const int PlayerAppearNapRomFrames = 6;

    /// <summary>How many fiftieths of a second the arcade then sleeps before it switches the game on.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>NAP 4,PLS2</c>.</item>
    ///         <item>Disassembly: <c>$2885</c>.</item>
    ///     </list>
    /// </remarks>
    private const int DiagonalPlayerAppearNapRomFrames = 4;

    /// <summary>How many fiftieths of a second after the wave is set up the game goes live.</summary>
    private readonly int _liveRomFrames;

    /// <summary>How many fiftieths of a second after the wave is set up the player appears.</summary>
    private readonly int _playerAppearRomFrames;

    /// <summary>Counts the time since the wave was set up, in clock units. It stops counting once the game is live.</summary>
    private int _clockUnits;

    /// <summary>True on the one tick on which the game goes live.</summary>
    private bool _hasJustGoneLive;

    /// <summary>
    ///     How many clock units of the latest tick came after the game went live: none before it, part of the tick on
    ///     which it goes live, and the whole tick from then on.
    /// </summary>
    private int _liveClockUnitsThisTick;

    /// <summary>Works out the times for one wave.</summary>
    /// <param name="robotListCount">
    ///     How many robots the arcade's appear loop would bring in: the ones on its robot list, which
    ///     are the grunts, the hulks and the tanks.
    /// </param>
    /// <param name="isBrainWave">
    ///     True on a brain wave, where the robots are beamed in and the times do not depend on how many
    ///     there are.
    /// </param>
    public WaveStartSequence(int robotListCount, bool isBrainWave)
    {
        _playerAppearRomFrames = isBrainWave
            ? BrainWavePlayerAppearRomFrames
            : GetAppearLoopRomFrames(robotListCount) + AppearFinishNapRomFrames + RobotsOnNapRomFrames;
        _liveRomFrames = _playerAppearRomFrames + PlayerAppearNapRomFrames + DiagonalPlayerAppearNapRomFrames;
    }

    /// <summary>How many fiftieths of a second after the wave is set up the game goes live (test hook).</summary>
    internal int LiveRomFrames => _liveRomFrames;

    /// <summary>How many fiftieths of a second after the wave is set up the player appears (test hook).</summary>
    internal int PlayerAppearRomFrames => _playerAppearRomFrames;

    /// <summary>Moves the count on by one port tick.</summary>
    public void Update()
    {
        if (IsLive())
        {
            _liveClockUnitsThisTick = ArcadeClock.UnitsPerPortTick;
            _hasJustGoneLive = false;
            return;
        }

        _clockUnits += ArcadeClock.UnitsPerPortTick;
        _liveClockUnitsThisTick = Math.Max(0, _clockUnits - ArcadeClock.ToClockUnits(_liveRomFrames));
        _hasJustGoneLive = IsLive();
    }

    /// <summary>
    ///     Gets how much of the latest tick was live play, in clock units, so that a timer that starts when the game goes
    ///     live starts at exactly that moment and not at the nearest tick.
    /// </summary>
    /// <returns>
    ///     The clock units: none before the game is live, the part of the tick left over on the tick it goes live, and a
    ///     whole tick's worth after that.
    /// </returns>
    public int GetLiveClockUnitsThisTick()
    {
        return _liveClockUnitsThisTick;
    }

    /// <summary>Says whether the player has started to appear. Before this the player is not on the screen.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLS1</c>, <c>JSR PAPPR</c>.</item>
    ///         <item>Disassembly: <c>$2874</c>.</item>
    ///     </list>
    /// </remarks>
    public bool HasPlayerAppeared()
    {
        return _clockUnits >= ArcadeClock.ToClockUnits(_playerAppearRomFrames);
    }

    /// <summary>
    ///     Gets how long ago the player started to appear, in clock units, so that the appear effect takes its first step
    ///     on the right fiftieth of a second.
    /// </summary>
    /// <returns>The clock units since then. It is less than nothing before the player has started to appear.</returns>
    public int GetClockUnitsSincePlayerAppeared()
    {
        return _clockUnits - ArcadeClock.ToClockUnits(_playerAppearRomFrames);
    }

    /// <summary>Says whether the second part of the player's appear has started, which is the strips that lean.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLS1A</c>, <c>JSR PDAPPR</c>.</item>
    ///         <item>Disassembly: <c>$2882</c>.</item>
    ///     </list>
    /// </remarks>
    public bool HasPlayerDiagonalAppearStarted()
    {
        return GetClockUnitsSincePlayerDiagonalAppearStarted() >= 0;
    }

    /// <summary>Gets how long ago the second part of the player's appear started, in clock units.</summary>
    /// <returns>The clock units since then. It is less than nothing before that part has started.</returns>
    public int GetClockUnitsSincePlayerDiagonalAppearStarted()
    {
        return _clockUnits - ArcadeClock.ToClockUnits(_playerAppearRomFrames + PlayerAppearNapRomFrames);
    }

    /// <summary>
    ///     Says whether this is the tick on which the game goes live. The playfield tells the robots on that tick, so
    ///     that each one can set the time of its first move.
    /// </summary>
    public bool HasJustGoneLive()
    {
        return _hasJustGoneLive;
    }

    /// <summary>
    ///     Works out how long a robot waits before its first move, counted from the start of the tick on which the game goes
    ///     live. A robot does not see the game go live at once.
    ///     It looks at intervals, and its looks are counted from when the wave was set up, so the first look that finds the
    ///     game live can be some frames after it went live.
    ///     Some robots then sleep once more before they move.
    /// </summary>
    /// <param name="pollRomFrames">How many fiftieths of a second the robot sleeps between one look and the next.</param>
    /// <param name="napRomFrames">
    ///     How many fiftieths of a second the robot sleeps after the look that finds the game live,
    ///     before its first move.
    /// </param>
    /// <returns>
    ///     The clock units from the start of this tick to the robot's first move. Call it only on the tick that
    ///     <see cref="HasJustGoneLive" /> is true.
    /// </returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRP8.ASM</c> <c>ROBOT</c> (
    ///             <c>
    ///                 BITA #$7F / BEQ
    ///                 ROB0A / NAP 2,ROBOT
    ///             </c>
    ///             , then <c>NAP 10,ROB0</c>), and the same test at the head of
    ///             <c>HULK</c>, <c>BRAIN</c> and <c>TANK</c>; <c>RRS22.ASM</c> <c>MKPRCV</c> links a new process
    ///             in straight after the one that made it, so each robot takes its first look on the frame the
    ///             wave is set up, after the routine that set it up
    ///         </item>
    ///         <item>Disassembly: the status byte at <c>$59</c></item>
    ///     </list>
    /// </remarks>
    public int GetClockUnitsToFirstBeat(int pollRomFrames, int napRomFrames)
    {
        var looksBeforeLive = (_liveRomFrames + pollRomFrames - 1) / pollRomFrames;
        var firstBeatRomFrames = looksBeforeLive * pollRomFrames + napRomFrames;
        var clockUnitsAtStartOfTick = _clockUnits - ArcadeClock.UnitsPerPortTick;
        return ArcadeClock.ToClockUnits(firstBeatRomFrames) - clockUnitsAtStartOfTick;
    }

    /// <summary>Says whether the game is live: the player can move and fire, things can touch the player, and the robots act.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLS2</c>, <c>MAKP LSPROC / MAKP COLCHK / CLR STATUS</c>.</item>
    ///         <item>Disassembly: <c>$288D</c> to <c>$289A</c>.</item>
    ///     </list>
    /// </remarks>
    public bool IsLive()
    {
        return _clockUnits >= ArcadeClock.ToClockUnits(_liveRomFrames);
    }

    /// <summary>
    ///     Makes the game live at once, for a test of something that happens in play and not at the start of a wave (test
    ///     hook).
    /// </summary>
    internal void SkipToLive()
    {
        _clockUnits = ArcadeClock.ToClockUnits(_liveRomFrames);
    }

    /// <summary>
    ///     Works out how many fiftieths of a second the arcade's appear loop runs for. Its first pass is made at once, so
    ///     it sleeps one time fewer than it makes passes.
    /// </summary>
    /// <param name="robotListCount">How many robots are on the arcade's robot list.</param>
    /// <returns>The fiftieths of a second from the first pass to the last.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>.</item>
    ///         <item>
    ///             Disassembly: <c>$2911</c> to <c>$2951</c>. From the pass after
    ///             <see
    ///                 cref="AppearExtraPasses" />
    ///             , each pass moves a marker one robot along the list, and the loop
    ///             ends on the pass that finds the marker at the end. With no robots at all the marker is at the
    ///             end straight away, which is the same count as for one robot.
    ///         </item>
    ///     </list>
    /// </remarks>
    private static int GetAppearLoopRomFrames(int robotListCount)
    {
        var passes = Math.Max(robotListCount, 1) + AppearExtraPasses;
        return (passes - 1) * AppearPassRomFrames;
    }
}
