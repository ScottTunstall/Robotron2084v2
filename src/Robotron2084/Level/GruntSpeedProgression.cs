using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>
/// The wave's grunt-speed floor (R5 $BE5D): the wave table's value (RMXSPD) at wave start, then lowered by the
/// level-progress task once fewer than thirty grunts are left, down to the arcade player's own speed (notes §31, §134).
/// </summary>
/// <remarks>
/// Original source: <c>RRG23.ASM</c> <c>GEXEC</c> (<c>GEXEC0</c> to <c>GEXEC4</c>). Disassembly: <c>$2A85</c> to <c>$2B08</c>.
/// </remarks>
public sealed class GruntSpeedProgression
{
    /// <summary>R5 $2A85-2B08: the internal counter starts at 18 passes of 15 ROM frames before the first update.</summary>
    private const int FirstUpdateRomFrames = 18 * 15;

    /// <summary>The ROM's <c>$FEFC</c> operand: the floor drops by 2 on a pass with no scoring before it.</summary>
    private const int LargeFloorStep = 2;

    /// <summary>The limit ($BE5C) drops by this many times the floor's step on the same pass.</summary>
    private const int LimitStepPerFloorStep = 2;

    /// <summary>The lowest the floor goes: 1 arcade pixel a frame, the arcade player's own speed ($3031 deltas are ±1).</summary>
    private const int LowestFloor = 1;

    /// <summary>R5 $2ACA (<c>CMPA #30 / BHS</c>): the update is skipped while this many grunts or more are alive.</summary>
    private const int GruntCountThatHoldsTheFloor = 30;

    /// <summary>The ROM's <c>$FFFE</c> operand: the floor drops by 1 on a pass that follows some scoring.</summary>
    private const int SmallFloorStep = 1;

    /// <summary>R5 $2A85-2B08: after the first update, the counter's 15 passes of 15 ROM frames.</summary>
    private const int UpdateIntervalRomFrames = 15 * 15;

    private bool _scoredSinceLastPass; // the ROM's SCRFLG ($F0), set by every score
    private int _updateTimer = ArcadeClock.ToPortTicks(FirstUpdateRomFrames);

    /// <summary>Starts a wave's progression at the wave table's floor.</summary>
    /// <param name="initialFloor">The wave table's RMXSPD.</param>
    public GruntSpeedProgression(int initialFloor) => Floor = initialFloor;

    /// <summary>Notes that the player has scored, so the next pass is gentler. A wave where nothing is scored is punished for stalling.</summary>
    /// <remarks>Original source: <c>RRS22.ASM</c> <c>SCOREV</c> (<c>INC SCRFLG</c>). Disassembly: <c>UPDATE_PLAYER_SCORE</c> (<c>$DB9C</c>).</remarks>
    public void NoteScore() => _scoredSinceLastPass = true;

    /// <summary>The current floor: a grunt's step delay never drops below it by a speed-up (R5 $BE5D).</summary>
    public int Floor { get; private set; }

    /// <summary>
    /// ROM grunt speedup (3A94-3A9F): the delay times 224/256 (TRUNCATED), applied ONLY while the result is still at
    /// or above the current floor. Applied to every surviving grunt, whose in-flight countdown is deliberately NOT
    /// touched (notes §67).
    /// </summary>
    /// <param name="grunts">The field's grunts.</param>
    public void SpeedUp(IEnumerable<Grunt> grunts)
    {
        foreach (Grunt grunt in grunts.Where(grunt => grunt.IsAlive()))
        {
            grunt.SpeedUp(Floor);
        }
    }

    /// <summary>
    /// R5 $2AC7-2AF1 "update grunt speed as level progresses": every 225 ROM frames (first at 270) and ONLY while
    /// fewer than 30 grunts are alive, the floor drops by 2 (and the limit by 4), or by 1 (and 2) if the player has
    /// scored since the last pass ("BONE HIM FOR STALLING"), and every live grunt's limit is clamped to the new floor.
    /// </summary>
    /// <param name="grunts">The field's grunts.</param>
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
