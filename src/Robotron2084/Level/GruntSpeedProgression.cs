using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>
///     Sets the speed limit for the grunts. As the wave goes on and fewer grunts are left, the limit is eased now and
///     then, so the grunts can get faster, until they are as fast as the player.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>GEXEC</c> (<c>GEXEC0</c> to
///             <c>GEXEC4</c>); the table value is <c>RMXSPD</c>
///         </item>
///         <item>Disassembly: <c>$2A85</c> to <c>$2B08</c>, working on <c>$BE5D</c></item>
///     </list>
///     A grunt has a
///     beat every few fiftieths of a second, and moves only after a random number of beats, so the fewer beats it
///     waits, the faster it is. Each time a grunt dies, the grunts that are left have their waits made
///     shorter. The floor is the fewest beats they may be brought down to, so it is the grunts' speed
///     limit. Lowering the floor eases that limit and lets them get faster. The wave starts at the floor
///     the wave table gives it (notes §31, §134). Every so often the game makes a check on the floor, and
///     fiftieths of a second are used in this class only to time those checks. The count of fiftieths of a second starts
///     when
///     the game goes live (the arcade's <c>CLR STATUS</c> at <c>PLS2</c>, when the player may move and
///     fire and the robots may act), so the playfield does not call <see cref="Update" /> before then. At
///     a check the game lowers the floor, unless many grunts are still alive. The waits and the floor
///     themselves are counted in beats.</item>
///     </list>
/// </remarks>
public sealed class GruntSpeedProgression
{
    /// <summary>
    ///     How many fiftieths of a second the game sleeps between one pass of its loop and the next. A pass is the unit
    ///     the game counts its checks in: <see cref="FirstCheckPasses" /> and <see cref="CheckIntervalPasses" /> are both
    ///     counted in passes.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>NAP 15,GEXEC0</c>.</item>
    ///         <item>Disassembly: <c>$2A85</c> to <c>$2B08</c>.</item>
    ///     </list>
    /// </remarks>
    private const int PassRomFrames = 15;

    /// <summary>
    ///     The pass on which the game makes its first check on whether to lower <see cref="Floor" />. The game makes its
    ///     first pass at once and then sleeps before each of the others, so one fewer than this many sleeps of
    ///     <see cref="PassRomFrames" /> go by before the first check. It is used to work out
    ///     <see cref="FirstCheckRomFrames" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>LDA #18 / STA PD,U</c>, counted down by
    ///             <c>DEC PD,U</c> on every pass, the first one included.
    ///         </item>
    ///         <item>Disassembly: <c>$2A87</c> and <c>$2ABF</c>.</item>
    ///     </list>
    /// </remarks>
    private const int FirstCheckPasses = 18;

    /// <summary>
    ///     How many passes the game counts between one check on whether to lower <see cref="Floor" /> and the next. With
    ///     <see cref="PassRomFrames" /> it gives <see cref="CheckIntervalRomFrames" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>LDA #15 / STA PD,U</c>.</item>
    ///         <item>Disassembly: <c>$2A85</c> to <c>$2B08</c>.</item>
    ///     </list>
    /// </remarks>
    private const int CheckIntervalPasses = 15;

    /// <summary>
    ///     How many fiftieths of a second the game sleeps after it goes live before it switches the robots on again. It
    ///     is the first part of <see cref="ExecutiveStartRomFrames" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>NAP 12,PLS3</c>, straight after <c>CLR STATUS</c> at
    ///             <c>PLS2</c>.
    ///         </item>
    ///         <item>Disassembly: <c>$289A</c> to <c>$28A1</c>.</item>
    ///     </list>
    /// </remarks>
    private const int Pls3NapRomFrames = 12;

    /// <summary>
    ///     How many fiftieths of a second the game then sleeps before it starts its loop of passes. It is the second part
    ///     of <see cref="ExecutiveStartRomFrames" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>NAP 10,PLS4</c>; <c>PLS4</c> runs on into
    ///             <c>
    ///                 JMP
    ///                 GEXEC
    ///             </c>
    ///             with no more sleeps.
    ///         </item>
    ///         <item>Disassembly: <c>$28AA</c> to <c>$28D7</c>.</item>
    ///     </list>
    /// </remarks>
    private const int Pls4NapRomFrames = 10;

    /// <summary>
    ///     How many fiftieths of a second go by, from when the game goes live, before the game starts its loop of passes.
    ///     When <see cref="_checkClockUnits" /> has counted this many, the loop has started, and a score made before then is
    ///     forgotten (<see cref="_hasPlayerScoredSinceLastCheck" />).
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>PLS2</c> to <c>HIDEND</c>; <c>GEXEC</c> opens with
    ///             <c>
    ///                 CLR
    ///                 SCRFLG
    ///             </c>
    ///             .
    ///         </item>
    ///         <item>Disassembly: <c>$289A</c> to <c>$2A8B</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ExecutiveStartRomFrames = Pls3NapRomFrames + Pls4NapRomFrames;

    /// <summary>
    ///     How many fiftieths of a second go by, from when the game goes live, before its first check on whether to lower
    ///     <see cref="Floor" />. It is the starting value of <see cref="_checkWaitRomFrames" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>PLS2</c> to <c>GEXEC1</c>: the two sleeps before the loop
    ///             starts, and then one sleep before every pass but the first.
    ///         </item>
    ///         <item>Disassembly: <c>$289A</c> to <c>$2AC5</c>.</item>
    ///     </list>
    /// </remarks>
    private const int FirstCheckRomFrames = ExecutiveStartRomFrames + (FirstCheckPasses - 1) * PassRomFrames;

    /// <summary>
    ///     The number of beats subtracted from <see cref="Floor" /> at a check when the player has scored nothing since
    ///     the previous check. A smaller <see cref="Floor" /> lets the grunts get faster.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FEFC</c>.</item>
    ///         <item>Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</item>
    ///     </list>
    /// </remarks>
    private const int LargeFloorStep = 2;

    /// <summary>
    ///     Each time <see cref="Floor" /> is lowered, every grunt's longest wait (<see cref="Grunt.MoveDelayBeats" />) is
    ///     cut as well. This many times the number of beats subtracted from <see cref="Floor" /> is subtracted from it.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which changes <c>ROBSPD</c> along with the
    ///             floor.
    ///         </item>
    ///         <item>Disassembly: <c>$BE5C</c>.</item>
    ///     </list>
    /// </remarks>
    private const int LimitStepPerFloorStep = 2;

    /// <summary>
    ///     The smallest value <see cref="Floor" /> can ever have, in beats. A grunt waiting only this long is as fast as
    ///     the player.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>.</item>
    ///         <item>Disassembly: the player's steps at <c>$3031</c>, which are one pixel.</item>
    ///     </list>
    /// </remarks>
    private const int LowestFloor = 1;

    /// <summary>
    ///     The number of live grunts at or above which a check leaves <see cref="Floor" /> alone. A check lowers the
    ///     floor only while fewer grunts than this are alive.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>CMPA #30 / BHS</c>.</item>
    ///         <item>Disassembly: <c>$2ACA</c>.</item>
    ///     </list>
    /// </remarks>
    private const int GruntCountThatHoldsTheFloor = 30;

    /// <summary>
    ///     The number of beats subtracted from <see cref="Floor" /> at a check when the player has scored at least once
    ///     since the previous check. It is smaller than <see cref="LargeFloorStep" />, so a player who keeps scoring has the
    ///     grunts speeded up more slowly.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FFFE</c>.</item>
    ///         <item>Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</item>
    ///     </list>
    /// </remarks>
    private const int SmallFloorStep = 1;

    /// <summary>
    ///     How many fiftieths of a second the game waits between one check on whether to lower <see cref="Floor" /> and
    ///     the next. It is the value <see cref="_checkWaitRomFrames" /> has after the first check.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down fifteen passes of fifteen
    ///             frames.
    ///         </item>
    ///         <item>Disassembly: <c>$2A85</c> to <c>$2B08</c>.</item>
    ///     </list>
    /// </remarks>
    private const int CheckIntervalRomFrames = CheckIntervalPasses * PassRomFrames;

    /// <summary>Counts up to the next check, in clock units. It only counts while the game is live.</summary>
    private int _checkClockUnits;

    /// <summary>How many fiftieths of a second <see cref="_checkClockUnits" /> counts up to before the next check.</summary>
    private int _checkWaitRomFrames = FirstCheckRomFrames;

    /// <summary>
    ///     True once the game has started its loop of passes, which is <see cref="ExecutiveStartRomFrames" /> after the
    ///     game goes live.
    /// </summary>
    private bool _hasExecutiveStarted;

    /// <summary>True if the player has scored at least once since the previous check.</summary>
    private bool _hasPlayerScoredSinceLastCheck;

    /// <summary>Starts a wave at the floor the wave table gives it.</summary>
    /// <param name="initialFloor">The wave table's floor.</param>
    public GruntSpeedProgression(int initialFloor)
    {
        Floor = initialFloor;
    }

    /// <summary>The floor: the fewest beats a grunt may be made to wait between moves. A smaller number is a faster grunt.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>RMXSPD</c>.</item>
    ///         <item>Disassembly: <c>$BE5D</c>.</item>
    ///     </list>
    /// </remarks>
    public int Floor { get; private set; }

    /// <summary>
    ///     Records that the player has scored, so the next check subtracts <see cref="SmallFloorStep" /> beats from the
    ///     floor instead of <see cref="LargeFloorStep" />. A player who scores nothing is punished for stalling, because the
    ///     floor falls further and the grunts get faster sooner.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRS22.ASM</c> <c>SCOREV</c> (<c>INC SCRFLG</c>).</item>
    ///         <item>Disassembly: <c>UPDATE_PLAYER_SCORE</c> (<c>$DB9C</c>).</item>
    ///     </list>
    /// </remarks>
    public void NoteScore()
    {
        _hasPlayerScoredSinceLastCheck = true;
    }

    /// <summary>
    ///     Makes every grunt that is still alive a little faster, as happens each time a grunt dies, by cutting how many
    ///     beats it may wait. A grunt that would be made faster than the floor allows is left as it is.
    /// </summary>
    /// <param name="grunts">The field's grunts.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRP8.ASM</c> <c>ROBK0</c> ("SPEED EM UP"),
    ///             which every grunt death runs
    ///         </item>
    ///         <item>Disassembly: <c>$3A94</c> to <c>$3A9F</c></item>
    ///     </list>
    ///     A grunt's longest wait is made a
    ///     little shorter, rounding down. The wait it is already part-way through is left alone (notes
    ///     §67).</item>
    ///     </list>
    /// </remarks>
    public void SpeedUp(IEnumerable<Grunt> grunts)
    {
        foreach (var grunt in grunts.Where(grunt => grunt.IsAlive())) grunt.SpeedUp(Floor);
    }

    /// <summary>
    ///     Counts the live play that has gone by. When the time is up, and the grunts have dropped below
    ///     <see cref="GruntCountThatHoldsTheFloor" />, it lowers the floor, so the grunts may get faster still.
    /// </summary>
    /// <param name="grunts">The field's grunts.</param>
    /// <param name="clockUnits">
    ///     How much live play has gone by since the last call, in clock units: a whole port tick's worth,
    ///     or less on the tick the game goes live.
    /// </param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the part that
    ///             updates the grunts' speed as the level progresses ("BONE HIM FOR STALLING")
    ///         </item>
    ///         <item>Disassembly: <c>$2AC7</c> to <c>$2AF1</c></item>
    ///     </list>
    ///     At each check,
    ///     <see
    ///         cref="LargeFloorStep" />
    ///     beats are subtracted from the floor, or <see cref="SmallFloorStep" />
    ///     beats if the player has scored since the previous check, but the floor never goes below
    ///     <see
    ///         cref="LowestFloor" />
    ///     . Each live grunt's longest random wait is cut too, by
    ///     <see
    ///         cref="LimitStepPerFloorStep" />
    ///     times as much, though never below the new floor.</item>
    ///     </list>
    /// </remarks>
    public void Update(EntityList<Grunt> grunts, int clockUnits)
    {
        _checkClockUnits += clockUnits;
        ForgetScoreWhenTheExecutiveStarts();
        if (_checkClockUnits < ArcadeClock.ToClockUnits(_checkWaitRomFrames)) return;

        _checkClockUnits -= ArcadeClock.ToClockUnits(_checkWaitRomFrames);
        _checkWaitRomFrames = CheckIntervalRomFrames;

        if (grunts.GetLiveCount() >= GruntCountThatHoldsTheFloor) return;

        var floorStep = _hasPlayerScoredSinceLastCheck ? SmallFloorStep : LargeFloorStep;
        _hasPlayerScoredSinceLastCheck = false;

        Floor = Math.Max(LowestFloor, Floor - floorStep);
        foreach (var grunt in grunts.Where(grunt => grunt.IsAlive()))
            grunt.WaveSpeedTick(Floor, LimitStepPerFloorStep * floorStep);
    }

    /// <summary>Forgets any score made before the game started its loop of passes, at the moment the loop starts.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>CLR SCRFLG</c>.</item>
    ///         <item>Disassembly: <c>$2A8B</c>.</item>
    ///     </list>
    /// </remarks>
    private void ForgetScoreWhenTheExecutiveStarts()
    {
        if (_hasExecutiveStarted || _checkClockUnits < ArcadeClock.ToClockUnits(ExecutiveStartRomFrames)) return;

        _hasExecutiveStarted = true;
        _hasPlayerScoredSinceLastCheck = false;
    }
}
