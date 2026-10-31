using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A hulk walked onto a family member.</summary>
/// <param name="Hulk">The hulk.</param>
/// <param name="FamilyMember">The family member it walked onto.</param>
internal sealed record HulkKilledFamilyMemberResult(Hulk Hulk, FamilyMember FamilyMember) : CollisionResult;
