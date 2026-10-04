using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>Decides how fast the grunts may get. As the wave goes on and fewer grunts are left, the fastest they may move gets faster, until the grunts are as fast as the player.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c> (<c>GEXEC0</c> to <c>GEXEC4</c>); the table value is <c>RMXSPD</c></item>
/// <item>Disassembly: <c>$2A85</c> to <c>$2B08</c>, working on <c>$BE5D</c></item>
/// </list>
/// A grunt's speed is how long it waits between moves, so a lower number is a faster grunt and the fastest allowed is a floor on that number.
/// The wave starts at the wave table's floor (notes §31, §134).
/// </remarks>
public sealed class GruntSpeedProgression
{
    /// <summary>How many ROM frames pass before the floor is first lowered.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down eighteen passes of fifteen frames. Disassembly: <c>$2A85</c> to <c>$2B08</c>.</remarks>
    private const int FirstUpdateRomFrames = 18 * 15;

    /// <summary>How far the floor drops on a pass when the player has not scored since the last one.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FEFC</c>. Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</remarks>
    private const int LargeFloorStep = 2;

    /// <summary>How many times the floor's drop the grunts' own limit falls by on the same pass.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which changes <c>ROBSPD</c> along with the floor. Disassembly: <c>$BE5C</c>.</remarks>
    private const int LimitStepPerFloorStep = 2;

    /// <summary>The lowest the floor goes. It is as fast as the player moves.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>. Disassembly: the player's steps at <c>$3031</c>, which are one pixel.</remarks>
    private const int LowestFloor = 1;

    /// <summary>The number of live grunts at or above which the floor is left alone.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, <c>CMPA #30 / BHS</c>. Disassembly: <c>$2ACA</c>.</remarks>
    private const int GruntCountThatHoldsTheFloor = 30;

    /// <summary>How far the floor drops on a pass when the player has scored since the last one.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the operand <c>$FFFE</c>. Disassembly: <c>$2AC7</c> to <c>$2AF1</c>.</remarks>
    private const int SmallFloorStep = 1;

    /// <summary>How many ROM frames pass between one lowering of the floor and the next.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, which counts down fifteen passes of fifteen frames. Disassembly: <c>$2A85</c> to <c>$2B08</c>.</remarks>
    private const int UpdateIntervalRomFrames = 15 * 15;

    /// <summary>True once the player has scored since the floor was last looked at.</summary>
    private bool _scoredSinceLastPass;

    /// <summary>Counts down to the next time the floor is looked at.</summary>
    private int _updateTimer = ArcadeClock.ToPortTicks(FirstUpdateRomFrames);

    /// <summary>Starts a wave at the floor the wave table gives it.</summary>
    /// <param name="initialFloor">The wave table's floor.</param>
    public GruntSpeedProgression(int initialFloor) => Floor = initialFloor;

    /// <summary>Notes that the player has scored, so the next drop in the floor is smaller. A player who scores nothing is punished for stalling.</summary>
    /// <remarks>Original source: <c>RRS22.ASM</c> <c>SCOREV</c> (<c>INC SCRFLG</c>). Disassembly: <c>UPDATE_PLAYER_SCORE</c> (<c>$DB9C</c>).</remarks>
    public void NoteScore() => _scoredSinceLastPass = true;

    /// <summary>The shortest wait between moves that a speed-up may give a grunt.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>RMXSPD</c>. Disassembly: <c>$BE5D</c>.</remarks>
    public int Floor { get; private set; }

    /// <summary>Speeds up every grunt that is still alive, as happens each time a grunt dies. A grunt that would go faster than the floor allows is left as it is.</summary>
    /// <param name="grunts">The field's grunts.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROBK0</c> ("SPEED EM UP"), which every grunt death runs</item>
    /// <item>Disassembly: <c>$3A94</c> to <c>$3A9F</c></item>
    /// </list>
    /// A grunt's wait is made a little shorter, rounding down. The wait it is already part-way through is left alone (notes §67).
    /// </remarks>
    public void SpeedUp(IEnumerable<Grunt> grunts)
    {
        foreach (Grunt grunt in grunts.Where(grunt => grunt.IsAlive()))
        {
            grunt.SpeedUp(Floor);
        }
    }

    /// <summary>Counts down one tick, and when the time is up lowers the floor, so the grunts may get faster. Nothing happens while many grunts are alive.</summary>
    /// <param name="grunts">The field's grunts.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>GEXEC</c>, the part that updates the grunts' speed as the level progresses ("BONE HIM FOR STALLING")</item>
    /// <item>Disassembly: <c>$2AC7</c> to <c>$2AF1</c></item>
    /// </list>
    /// The floor drops by <see cref="LargeFloorStep"/>, or by <see cref="SmallFloorStep"/> if the player has scored since the last time. Every live grunt's own limit
    /// is then brought down to the new floor.
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
