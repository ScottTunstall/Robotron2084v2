using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's hulks on the field, not too near where the player starts, and gives each one something to stalk.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c> <c>HULKST</c>; the beat interval is the wave's <c>HLKSPD</c></item>
/// <item>Disassembly: <c>HULK_INITIALISE</c> (<c>$017C</c>)</item>
/// </list>
/// Half the hulks stalk the last family member to join, and go for the player once that member is gone
/// (<c>$010D</c>, <c>$0113</c>). The other half were meant to stalk a member found by a search, but the hulks are made
/// before the family, so the search finds nothing. The ROM then chases a phantom object; this port takes the intended
/// fall back to the player instead.
/// </remarks>
public sealed class HulkWaveSpawner : IWaveSpawner
{
    /// <summary>How many ways the roll for what a hulk stalks can come up.</summary>
    private const int TargetRollSides = 2;

    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        FieldEntities entities = context.Entities;
        SpawnPlacement placement = context.Placement;
        for (int i = 0; i < field.Parameters.HulkCount; i++)
        {
            IntVector2 position = placement.FindSpawnPointAwayFrom(context.PlayerStart, SpawnTuning.HulkMinDistanceFromPlayer);
            Func<IntVector2> getTargetPosition = context.Random.Next(TargetRollSides) == 0
                ? () => entities.GetLastFamilyMemberPosition() ?? field.GetPlayerPosition()
                : () => field.GetPlayerPosition();
            var hulk = new Hulk(field.Sprites, position, context.Random, field.Parameters.HulkBeatIntervalRomFrames, getTargetPosition);
            entities.Add(hulk);
        }
    }
}
