using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>
///     Puts back the tanks that were alive when the player last died, each at full size and not too near where the
///     player starts.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRTK4.ASM</c> <c>TNKSTV</c></item>
///         <item>Disassembly: <c>asm/robomame.asm</c>, the tank start routine</item>
///     </list>
///     The wave table never brings tanks: a quark drops them. They are here because a death keeps the robots that were
///     left (notes §134).
/// </remarks>
public sealed class TankWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext context)
    {
        var field = context.Field;
        for (var i = 0; i < field.Parameters.TankCount; i++)
        {
            var position =
                context.Placement.FindSpawnPointAwayFrom(context.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer);
            var tank = new Tank(field.Sprites, position, context.Random, field.Parameters.TankFireDelay, true);
            context.Entities.Add(tank);
        }
    }
}
