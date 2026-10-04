using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>Sets the speed limit for the grunts. As the wave goes on and fewer grunts are left, the limit is eased now and then, so the grunts can get faster, until they are as fast as the player.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c> (<c>GEXEC0</c> to <c>GEXEC4</c>); the table value is <c>RMXSPD</c></item>
/// <item>Disassembly: <c>$2A85</c> to <c>$2B08</c>, working on <c>$BE5D</c></item>
/// </list>
/// A grunt has a beat every few ROM frames, and moves only after a random number of beats, so the fewer beats it waits, the faster it is. Each time a grunt dies, the
/// grunts that are left have their waits made shorter. The floor is the fewest beats they may be brought down to, so it is the grunts' speed limit. Lowering the floor eases
/// that limit and lets them get faster. The wave starts at the floor the wave table gives it (notes §31, §134).
/// Every so often the game makes a check on the floor, and ROM frames are used in this class only to time those checks. At a check the game lowers the floor,
/// unless many grunts are still alive. The waits and the floor themselves are counted in beats.
/// </remarks>
public sealed class GruntSpeedProgression
{
    /// <summary>How many ROM frames the game waits, from the start of the wave, before its first check on whether to lower <see cref="Floor"/>. It is the starting value of <see cref="_updateTimer"/>, after conversion to port ticks.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down eighteen passes of fifteen frames. Disassembly: <c>$2A85</c> to <c>$2B08</c>.</remarks>
    private const int FirstUpdateRomFrames = 18 * 15;

    /// <summary>The number of beats subtracted from <see cref="Floor"/> at a check when the player has scored nothing since the previous check. A smaller <see cref="Floor"/> lets the grunts get faster.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FEFC</c>. Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</remarks>
    private const int LargeFloorStep = 2;

    /// <summary>Each time <see cref="Floor"/> is lowered, every grunt's longest wait (<see cref="Grunt.MoveDelayBeats"/>) is cut as well. This many times the number of beats subtracted from <see cref="Floor"/> is subtracted from it.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which changes <c>ROBSPD</c> along with the floor. Disassembly: <c>$BE5C</c>.</remarks>
    private const int LimitStepPerFloorStep = 2;

    /// <summary>The smallest value <see cref="Floor"/> can ever have, in beats. A grunt waiting only this long is as fast as the player.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>. Disassembly: the player's steps at <c>$3031</c>, which are one pixel.</remarks>
    private const int LowestFloor = 1;

    /// <summary>If this many grunts or more are alive at a check, <see cref="Floor"/> is not lowered. It is only lowered at a check when fewer grunts than this are alive.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>CMPA #30 / BHS</c>. Disassembly: <c>$2ACA</c>.</remarks>
    private const int GruntCountThatHoldsTheFloor = 30;

    /// <summary>The number of beats subtracted from <see cref="Floor"/> at a check when the player has scored at least once since the previous check. It is smaller than <see cref="LargeFloorStep"/>, so a player who keeps scoring has the grunts speeded up more slowly.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FFFE</c>. Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</remarks>
    private const int SmallFloorStep = 1;

    /// <summary>How many ROM frames the game waits between one check on whether to lower <see cref="Floor"/> and the next. It is put back into <see cref="_updateTimer"/> after each check, after conversion to port ticks.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down fifteen passes of fifteen frames. Disassembly: <c>$2A85</c> to <c>$2B08</c>.</remarks>
    private const int UpdateIntervalRomFrames = 15 * 15;

    /// <summary>True if the player has scored at least once since the previous check.</summary>
    private bool _scoredSinceLastPass;

    /// <summary>Counts down port ticks to the next check.</summary>
    private int _updateTimer = ArcadeClock.ToPortTicks(FirstUpdateRomFrames);

    /// <summary>Starts a wave at the floor the wave table gives it.</summary>
    /// <param name="initialFloor">The wave table's floor.</param>
    public GruntSpeedProgression(int initialFloor) => Floor = initialFloor;

    /// <summary>Records that the player has scored, so the next check subtracts <see cref="SmallFloorStep"/> beats from the floor instead of <see cref="LargeFloorStep"/>. A player who scores nothing is punished for stalling, because the floor falls further and the grunts get faster sooner.</summary>
    /// <remarks>Original source: <c>RRS22.ASM</c> <c>SCOREV</c> (<c>INC SCRFLG</c>). Disassembly: <c>UPDATE_PLAYER_SCORE</c> (<c>$DB9C</c>).</remarks>
    public void NoteScore() => _scoredSinceLastPass = true;

    /// <summary>The floor: the fewest beats a grunt may be made to wait between moves. A smaller number is a faster grunt.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>RMXSPD</c>. Disassembly: <c>$BE5D</c>.</remarks>
    public int Floor { get; private set; }

    /// <summary>Makes every grunt that is still alive a little faster, as happens each time a grunt dies, by cutting how many beats it may wait. A grunt that would be made faster than the floor allows is left as it is.</summary>
    /// <param name="grunts">The field's grunts.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROBK0</c> ("SPEED EM UP"), which every grunt death runs</item>
    /// <item>Disassembly: <c>$3A94</c> to <c>$3A9F</c></item>
    /// </list>
    /// A grunt's longest wait is made a little shorter, rounding down. The wait it is already part-way through is left alone (notes §67).
    /// </remarks>
    public void SpeedUp(IEnumerable<Grunt> grunts)
    {
        foreach (Grunt grunt in grunts.Where(grunt => grunt.IsAlive()))
        {
            grunt.SpeedUp(Floor);
        }
    }

    /// <summary>Counts down one tick. When the time is up, and fewer than thirty grunts are left, it lowers the floor, so the grunts may get faster still.</summary>
    /// <param name="grunts">The field's grunts.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the part that updates the grunts' speed as the level progresses ("BONE HIM FOR STALLING")</item>
    /// <item>Disassembly: <c>$2AC7</c> to <c>$2AF1</c></item>
    /// </list>
    /// At each check, <see cref="LargeFloorStep"/> beats are subtracted from the floor, or <see cref="SmallFloorStep"/> beats if the player has scored since the previous check, but the floor never goes below <see cref="LowestFloor"/>.
    /// Each live grunt's longest random wait is cut too, by <see cref="LimitStepPerFloorStep"/> times as much, though never below the new floor.
    /// </remarks>
    public void Update(IEnumerable<Grunt> grunts)
    {
        if (--_updateTimer > 0)
        {
            return;
        }

        _updateTimer = ArcadeClock.ToPortTicks(UpdateIntervalRomFrames);

        if (grunts.Count(grunt => !grunt.IsDead()) >= GruntCountThatHoldsTheFloor)
        {
            return;
        }

        int floorStep = _scoredSinceLastPass ? SmallFloorStep : LargeFloorStep;
        _scoredSinceLastPass = false;

        Floor = Math.Max(LowestFloor, Floor - floorStep);
        foreach (Grunt grunt in grunts.Where(grunt => grunt.IsAlive()))
        {
            grunt.WaveSpeedTick(Floor, LimitStepPerFloorStep * floorStep);
        }
    }
}
