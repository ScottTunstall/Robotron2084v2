using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>
///     The player walking onto an electrode. Both the player and the electrode die, unless the player cannot be hurt
///     just now.
/// </summary>
internal sealed class PlayerVsElectrodeCollisionRule : ICollisionRule
{
    /// <inheritdoc />
    public IEnumerable<CollisionResult> Detect(ICollisionScene scene, FieldEntities entities)
    {
        if (!scene.CanPlayerBeHurt()) yield break;

        var touched =
            entities.Electrodes.FirstOrDefault(electrode => electrode.IsAlive() && scene.TouchesPlayer(electrode));
        if (touched is not null) yield return new PlayerHitElectrodeResult(touched);
    }
}
