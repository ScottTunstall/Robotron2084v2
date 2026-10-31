using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain caught the family member it was chasing.</summary>
/// <param name="Brain">The brain.</param>
/// <param name="FamilyMember">The family member it caught.</param>
internal sealed record BrainCaughtFamilyMemberResult(Brain Brain, FamilyMember FamilyMember) : CollisionResult;
