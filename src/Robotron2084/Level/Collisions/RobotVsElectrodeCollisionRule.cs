using Robotron2084.Audio;
using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A grunt or a hulk walking onto an electrode. The grunt and the electrode both die; a hulk destroys the electrode and walks on.</summary>
/// <remarks>Original source: <c>RRP8.ASM</c> <c>PSTKIL</c>. The electrode shrivels and never bursts, and every grunt that
/// dies speeds up the grunts that are left.</remarks>
public sealed class RobotVsElectrodeCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field, FieldEntities entities)
    {
        ResolveGrunts(field, entities);
        ResolveHulks(field, entities);
    }

    /// <summary>Finds the first living electrode an entity is touching.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="entities">What is on the field.</param>
    /// <param name="walker">The grunt or hulk.</param>
    /// <returns>The electrode, or null when it touches none.</returns>
    private static Electrode? FindTouchedElectrode(PlayField field, FieldEntities entities, IEntity walker) =>
        entities.Electrodes.FirstOrDefault(electrode => electrode.IsAlive() && field.Touches(walker, electrode));

    /// <summary>Kills each grunt that has walked onto an electrode, and the electrode with it.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="entities">What is on the field.</param>
    private static void ResolveGrunts(PlayField field, FieldEntities entities)
    {
        foreach (Grunt grunt in entities.Grunts)
        {
            if (!grunt.IsAlive() || FindTouchedElectrode(field, entities, grunt) is not { } electrode)
            {
                continue;
            }

            grunt.Kill();
            field.SpawnExplosion(grunt, null);
            electrode.Kill();
            field.SpeedUpGrunts();
            // PSTKIL asks for PSKSND, then ROBKIL for RBSND in the same frame; the voice keeps the first.
            field.PlaySoundFrom(SoundTables.PostKill, electrode.Bounds);
            field.PlaySoundFrom(SoundTables.RobotHit, grunt.Bounds);
        }
    }

    /// <summary>Destroys the electrode each hulk has walked onto. The hulk is not harmed.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="entities">What is on the field.</param>
    private static void ResolveHulks(PlayField field, FieldEntities entities)
    {
        foreach (Hulk hulk in entities.Hulks)
        {
            if (FindTouchedElectrode(field, entities, hulk) is { } electrode)
            {
                electrode.Kill();
                field.PlaySoundFrom(SoundTables.PostKill, electrode.Bounds);
            }
        }
    }
}
