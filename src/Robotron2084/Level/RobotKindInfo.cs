using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning;

namespace Robotron2084.Level;

/// <summary>Everything the playfield needs to know about one kind of robot: how many a wave has, what it is worth, how it is put on the field, what a laser does to it and whether touching it kills the player.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: each kind's own routines, such as <c>RRP8.ASM</c> for the grunt and <c>RRH11.ASM</c> for the hulk; this record gathers what they decide</item>
/// <item>Disassembly: not separately labelled, since it is the port's own registry of them</item>
/// </list>
/// The wave counts come from <see cref="LevelParameters"/>, the scores from <see cref="ScoreValues"/> (notes §11.3), and
/// <see cref="LaserHit"/> from the part of each kind's routine that handles a laser (notes §61, §64).
/// </remarks>
/// <param name="Kind">Which kind this row describes.</param>
/// <param name="WaveCount">How many of them the wave table brings; null when only another robot makes them.</param>
/// <param name="Score">The points a laser kill is worth. It is nothing for a robot that cannot be killed.</param>
/// <param name="LaserHit">What one laser does to one of them, from the kill to the burst it leaves.</param>
/// <param name="LaserHitSound">The sound made when a laser hits one.</param>
/// <param name="KillsPlayerOnContact">True when touching it kills the player.</param>
/// <param name="Spawn">How the wave's own are put on the field; null when only another robot makes them.</param>
/// <param name="IsChasedByDemoPlayer">True when the player in the attract demo steers towards it. It is false for the electrodes and the shots that the demo player dodges.</param>
/// <param name="IsOnRobotList">True when the arcade keeps this kind on its robot list (<c>GETROB</c>), which is the list its appear loop walks at the start of a wave. The number of robots on that list sets when the player appears and the game goes live (<see cref="WaveStartSequence"/>).</param>
public sealed record RobotKindInfo(
    RobotKind Kind,
    Func<LevelParameters, int>? WaveCount,
    int Score,
    Action<PlayField, IEntity, Direction8> LaserHit,
    SoundSequence LaserHitSound,
    bool KillsPlayerOnContact = false,
    IWaveSpawner? Spawn = null,
    bool IsChasedByDemoPlayer = true,
    bool IsOnRobotList = false);
