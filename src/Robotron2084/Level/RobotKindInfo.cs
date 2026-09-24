using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>
/// Everything the playfield needs to know about ONE robot kind: how many the wave brings, what it is worth, how
/// it is spawned, what a laser does to it and whether touching it is fatal.
/// </summary>
/// <remarks>
/// This is the port's registry of the arcade's robot routines: the wave counts come from <see cref="LevelParameters"/>
/// (the ROM's own wave tables), the scores from <see cref="ScoreValues"/> (notes §11.3), the spawn from the kind's
/// own initialise routine (plan 9.1), and <see cref="LaserHit"/> from the collision phase that kind's routine runs
/// (plan 9.2, notes §61, §64).
/// </remarks>
/// <param name="Kind">Which kind this row describes.</param>
/// <param name="WaveCount">How many of them the wave table brings; null when only another robot makes them.</param>
/// <param name="Score">Points a laser kill is worth (0 for what cannot be killed).</param>
/// <param name="LaserHit">What one laser does to one of them — the kind's whole phase, from the kill to its own burst.</param>
/// <param name="KillsPlayerOnContact">True when touching it kills the player.</param>
/// <param name="Spawn">Builds the wave's own at a chosen spot; null when only another robot makes them.</param>
public sealed record RobotKindInfo(
    RobotKind Kind,
    Func<LevelParameters, int>? WaveCount,
    int Score,
    Action<PlayField, IEntity, Direction8> LaserHit,
    bool KillsPlayerOnContact = false,
    Action<PlayField, IntVector2>? Spawn = null);
