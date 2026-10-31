using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>
///     The player touching anything that is deadly to touch: the walking robots and every enemy shot. Only the player
///     dies.
/// </summary>
/// <remarks>
///     Which kinds are deadly comes from <see cref="RobotKindInfo.KillsPlayerOnContact" />. The thing touched is not
///     removed; only a laser removes a shot.
/// </remarks>
internal sealed class PlayerContactKillRule : ICollisionRule
{
    /// <inheritdoc />
    public IEnumerable<CollisionResult> Detect(ICollisionScene collisionScene, FieldEntities fieldEntities)
    {
        if (!collisionScene.CanPlayerBeHurt()) yield break;

        foreach (var robot in RobotKinds.All.Where(kind => kind.KillsPlayerOnContact))
        {
            var touched = fieldEntities.GetEntities(robot.Kind)
                .FirstOrDefault(entity => entity.IsAlive() && collisionScene.TouchesPlayer(entity));
            if (touched is not null)
            {
                yield return new PlayerTouchedDeadThingResult(touched);
                yield break;
            }
        }
    }
}
