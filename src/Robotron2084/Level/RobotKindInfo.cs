using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning;

namespace Robotron2084.Level;

/// <summary>Everything the playfield needs to know about one kind of robot: how many a wave has, what it is worth, how it is put on the field, what a laser does to it and whether touching it kills the player.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: each kind's own routines, such as <c>RRP8.ASM</c> for
/// the grunt and <c>RRH11.ASM</c> for the hulk; this record gathers what they decide</item>
/// <item>Disassembly: not separately labelled, since it is the port's own registry of them</item>
/// </list>
/// The wave counts come from <see cref="LevelParameters"/>, the scores from <see cref="ScoreValues"/>
/// (notes §11.3), and <see cref="LaserHit"/> from the part of each kind's routine that handles a
/// laser (notes §61, §64).</item>
/// </list>
/// </remarks>
/// <param name="Kind">Which kind this row describes.</param>
/// <param name="WaveCount">How many of them the wave table brings; null when only another robot makes them.</param>
/// <param name="Score">The points a laser kill is worth. It is nothing for a robot that cannot be killed.</param>
/// <param name="LaserHit">What one laser does to one of them, from the kill to the burst it leaves.</param>
/// <param name="LaserHitSound">The sound made when a laser hits one.</param>
/// <param name="KillsPlayerOnContact">True when touching it kills the player.</param>
/// <param name="Spawn">How the wave's own are put on the field; null when only another robot makes them.</param>
/// <param name="IsChasedByDemoPlayer">True when the player in the attract demo steers towards it. It is false for the electrodes and the shots that the demo player dodges.</param>
/// <param name="RobotListSetUpOrder">
/// Where this kind comes in the order the arcade sets the robots on its robot list up in at the start of a wave: the kind with the lowest number is set up first.
/// It is null for a kind the arcade does not keep on that list. The robot list is the one the arcade's appear loop walks (<c>GETROB</c>), and each robot is put at its head,
/// so the loop meets the robots in the opposite order to the one they were set up in (<see cref="WaveMaterialisation"/>). The number of robots on the list sets when the player appears and the game goes live (<see cref="WaveStartSequence"/>).
/// </param>
public sealed record RobotKindInfo(
    RobotKind Kind,
    Func<LevelParameters, int>? WaveCount,
    int Score,
    Action<PlayField, IEntity, Direction8> LaserHit,
    SoundSequence LaserHitSound,
    bool KillsPlayerOnContact = false,
    IWaveSpawner? Spawn = null,
    bool IsChasedByDemoPlayer = true,
    int? RobotListSetUpOrder = null)
{
    /// <summary>Says whether the arcade keeps this kind on its robot list, which is the list its appear loop walks at the start of a wave.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRS22.ASM</c> <c>GETRBV</c>, which links a new robot in at the head of
    /// <c>RPTR</c>.</item>
    /// <item>Disassembly: the list at <c>$9821</c>.</item>
    /// </list>
    /// </remarks>
    public bool IsOnRobotList() => RobotListSetUpOrder is not null;
}
