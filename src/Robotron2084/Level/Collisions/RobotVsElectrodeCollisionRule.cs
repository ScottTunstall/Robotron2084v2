using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A grunt or a hulk walking onto an electrode. The grunt and the electrode both die; a hulk destroys the electrode and walks on.</summary>
/// <remarks>Original source: <c>RRP8.ASM</c> <c>PSTKIL</c>. The electrode shrivels and never bursts, and every grunt that
/// dies speeds up the grunts that are left.</remarks>
internal sealed class RobotVsElectrodeCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public IEnumerable<CollisionResult> Detect(ICollisionScene scene, FieldEntities entities)
    {
        foreach (Grunt grunt in entities.Grunts)
        {
            if (grunt.IsAlive() && FindTouchedElectrode(scene, entities, grunt) is { } electrode)
            {
                yield return new GruntHitElectrodeResult(grunt, electrode);
            }
        }

        foreach (Hulk hulk in entities.Hulks)
        {
            if (FindTouchedElectrode(scene, entities, hulk) is { } electrode)
            {
                yield return new HulkHitElectrodeResult(hulk, electrode);
            }
        }
    }

    /// <summary>Finds the first living electrode an entity is touching.</summary>
    /// <param name="scene">What the rule may ask about the field.</param>
    /// <param name="entities">What is on the field.</param>
    /// <param name="walker">The grunt or hulk.</param>
    /// <returns>The electrode, or null when it touches none.</returns>
    private static Electrode? FindTouchedElectrode(ICollisionScene scene, FieldEntities entities, IEntity walker) =>
        entities.Electrodes.FirstOrDefault(electrode => electrode.IsAlive() && scene.Touches(walker, electrode));
}
