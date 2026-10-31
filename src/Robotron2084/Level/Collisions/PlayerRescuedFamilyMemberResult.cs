using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player touched a family member.</summary>
/// <param name="FamilyMember">The family member the player touched.</param>
internal sealed record PlayerRescuedFamilyMemberResult(FamilyMember FamilyMember) : CollisionResult;
