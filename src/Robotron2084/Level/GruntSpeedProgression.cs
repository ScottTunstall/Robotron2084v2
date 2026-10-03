using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>
/// The wave's grunt-speed floor (R5 $BE5D): the wave table's value (RMXSPD) at wave start, then descended by the
/// level-progress task while many grunts are alive, down to the arcade player's own speed (notes §31).
/// </summary>
public sealed class GruntSpeedProgression
{
    /// <summary>R5 $2A85-2B08: the internal counter starts at 18 passes of 15 ROM frames before the first update.</summary>
    private const int FirstUpdateRomFrames = 18 * 15;

    /// <summary>The ROM's <c>$FEFC</c> operand: the floor drops by 2 on this pass.</summary>
    private const int LargeFloorStep = 2;

    /// <summary>The limit ($BE5C) drops by this many times the floor's step on the same pass.</summary>
    private const int LimitStepPerFloorStep = 2;

    /// <summary>The lowest the floor goes: 1 arcade pixel a frame, the arcade player's own speed ($3031 deltas are ±1).</summary>
    private const int LowestFloor = 1;

    /// <summary>R5 $2AC7: the update only runs while this many grunts are alive.</summary>
    private const int MinimumGruntsToProgress = 30;

    /// <summary>The ROM's <c>$FFFE</c> operand: the floor drops by 1 on this pass.</summary>
    private const int SmallFloorStep = 1;

    /// <summary>R5 $2A85-2B08: after the first update, the counter's 15 passes of 15 ROM frames.</summary>
    private const int UpdatePeriodRomFrames = 15 * 15;

    private int _floorStep = LargeFloorStep; // the ROM's $F0 toggle: -2/-4 then -1/-2
    private int _updateTimer = ArcadeClock.ToPortTicks(FirstUpdateRomFrames);

    /// <summary>Starts a wave's progression at the wave table's floor.</summary>
    /// <param name="initialFloor">The wave table's RMXSPD.</param>
    public GruntSpeedProgression(int initialFloor) => Floor = initialFloor;

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
        foreach (Grunt grunt in grunts.Where(grunt => grunt.LifeState == EntityLifeState.Alive))
        {
            grunt.SpeedUp(Floor);
        }
    }

    /// <summary>
    /// R5 $2AC7-2AF1 "update grunt speed as level progresses": every 225 ROM frames (first at 270) and ONLY while
    /// 30+ grunts are alive, the floor drops by 2 (and the limit by 4) on one pass, then by 1 (and 2) on the next,
    /// and every live grunt's limit is clamped to the new floor.
    /// </summary>
    /// <param name="grunts">The field's grunts.</param>
    public void Update(IEnumerable<Grunt> grunts)
    {
        if (--_updateTimer > 0)
        {
            return;
        }

        _updateTimer = ArcadeClock.ToPortTicks(UpdatePeriodRomFrames);

        if (grunts.Count(grunt => grunt.LifeState != EntityLifeState.Dead) < MinimumGruntsToProgress)
        {
            return;
        }

        Floor = Math.Max(LowestFloor, Floor - _floorStep);
        foreach (Grunt grunt in grunts.Where(grunt => grunt.LifeState == EntityLifeState.Alive))
        {
            grunt.WaveSpeedTick(Floor, LimitStepPerFloorStep * _floorStep);
        }

        _floorStep = _floorStep == LargeFloorStep ? SmallFloorStep : LargeFloorStep;
    }
}
