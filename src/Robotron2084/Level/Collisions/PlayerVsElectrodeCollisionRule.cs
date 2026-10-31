using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>
///     The player walking onto an electrode. Both the player and the electrode die, unless the player cannot be hurt
///     just now.
/// </summary>
internal sealed class PlayerVsElectrodeCollisionRule : ICollisionRule
{
    /// <inheritdoc />
    public IEnumerable<CollisionResult> Detect(ICollisionScene collisionScene, FieldEntities fieldEntities)
    {
        if (!collisionScene.CanPlayerBeHurt()) yield break;

        var touched =
            fieldEntities.Electrodes.FirstOrDefault(electrode => electrode.IsAlive() && collisionScene.TouchesPlayer(electrode));
        if (touched is not null) yield return new PlayerHitElectrodeResult(touched);
    }
}
