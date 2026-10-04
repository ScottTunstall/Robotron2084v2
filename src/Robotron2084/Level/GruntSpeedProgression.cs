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
/// that limit and lets them get faster. The wave starts at the floor the wave table gives it (notes §31, §134). ROM frames come into this class only for the timing of the
/// checks on the floor; the waits and the floor are counted in beats.
/// </remarks>
public sealed class GruntSpeedProgression
{
    /// <summary>How many ROM frames pass before the floor is first looked at to see whether it can be lowered.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down eighteen passes of fifteen frames. Disassembly: <c>$2A85</c> to <c>$2B08</c>.</remarks>
    private const int FirstUpdateRomFrames = 18 * 15;

    /// <summary>How many beats the floor is lowered by when the player has not scored since the last time it was looked at.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FEFC</c>. Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</remarks>
    private const int LargeFloorStep = 2;

    /// <summary>How many times as much as the floor is lowered by the longest wait a grunt may pick is also cut, each time the floor is lowered.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which changes <c>ROBSPD</c> along with the floor. Disassembly: <c>$BE5C</c>.</remarks>
    private const int LimitStepPerFloorStep = 2;

    /// <summary>The lowest the floor goes. It is as fast as the player moves.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>. Disassembly: the player's steps at <c>$3031</c>, which are one pixel.</remarks>
    private const int LowestFloor = 1;

    /// <summary>How many grunts must be alive, or more, for the floor to be left alone. The floor is only lowered when fewer than this are left.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>CMPA #30 / BHS</c>. Disassembly: <c>$2ACA</c>.</remarks>
    private const int GruntCountThatHoldsTheFloor = 30;

    /// <summary>How many beats the floor is lowered by when the player has scored since the last time it was looked at.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FFFE</c>. Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</remarks>
    private const int SmallFloorStep = 1;

    /// <summary>How many ROM frames pass between one look at the floor and the next.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down fifteen passes of fifteen frames. Disassembly: <c>$2A85</c> to <c>$2B08</c>.</remarks>
    private const int UpdateIntervalRomFrames = 15 * 15;

    /// <summary>True once the player has scored since the floor was last looked at.</summary>
    private bool _scoredSinceLastPass;

    /// <summary>Counts down to the next time the floor is looked at.</summary>
    private int _updateTimer = ArcadeClock.ToPortTicks(FirstUpdateRomFrames);

    /// <summary>Starts a wave at the floor the wave table gives it.</summary>
    /// <param name="initialFloor">The wave table's floor.</param>
    public GruntSpeedProgression(int initialFloor) => Floor = initialFloor;

    /// <summary>Notes that the player has scored, so the next time the floor is lowered it is lowered by less. A player who scores nothing is punished for stalling, because the grunts get faster sooner.</summary>
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
    /// The floor is lowered by <see cref="LargeFloorStep"/>, or by <see cref="SmallFloorStep"/> if the player has scored since the last time, but never below <see cref="LowestFloor"/>.
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
