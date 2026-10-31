namespace Robotron2084.Level.Collisions;

/// <summary>
///     The player touching a human rescues them. The player scores a bonus that grows with each rescue, and the bonus
///     is shown where the human stood.
/// </summary>
/// <remarks>
///     Original source: <c>RRG23.ASM</c> <c>COLCHK</c>, and <c>RRH11.ASM</c> <c>HUMKIL</c> for the score shown. The
///     touch leaves <c>PCFLG</c> set, so it is a bonus and no skull, and the player is not harmed.
/// </remarks>
internal sealed class PlayerRescueRule : ICollisionRule
{
    /// <inheritdoc />
    public IEnumerable<CollisionResult> Detect(ICollisionScene collisionScene, FieldEntities fieldEntities)
    {
        foreach (var human in fieldEntities.GetFamilyMembers())
            if (human.IsGraspable() && collisionScene.IsPlayerAlive() && collisionScene.TouchesPlayer(human))
                yield return new PlayerRescuedHumanResult(human);
    }
}
