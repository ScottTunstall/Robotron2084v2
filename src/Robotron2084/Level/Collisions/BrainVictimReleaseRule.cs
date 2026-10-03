using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain shot while it was reprogramming a human. The human is lost, no prog appears, and a skull is left where they stood.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRB10.ASM</c> <c>BRNKIL</c>, which checks whether the brain was past <c>BMUT3</c> and, if so, frees the human and drops a skull</item>
/// <item>Disassembly: <c>BRAIN_COLLISION_HANDLER</c> (<c>$1DD6</c>)</item>
/// </list>
/// </remarks>
public sealed class BrainVictimReleaseRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field)
    {
        foreach (Brain brain in field.Entities.Brains)
        {
            if (brain.LifeState != EntityLifeState.Alive && brain.ReleaseVictim() is { } released)
            {
                field.LeaveSkull(released.Position);
            }
        }
    }
}
