using Robotron2084.Audio;
using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A grunt or a hulk walking onto an electrode. The grunt and the electrode both die; a hulk destroys the electrode and walks on.</summary>
/// <remarks>Original source: <c>RRP8.ASM</c> <c>PSTKIL</c>. The electrode shrivels and never bursts, and every grunt that
/// dies speeds up the grunts that are left.</remarks>
public sealed class RobotVsElectrodeCollisionPhase : ICollisionPhase
{
    /// <inheritdoc/>
    public void Resolve(PlayField field)
    {
        ResolveGrunts(field);
        ResolveHulks(field);
    }

    /// <summary>Finds the first living electrode an entity is touching.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="walker">The grunt or hulk.</param>
    /// <returns>The electrode, or null when it touches none.</returns>
    private static Electrode? FindTouchedElectrode(PlayField field, IEntity walker) =>
        field.Entities.Electrodes.FirstOrDefault(electrode => electrode.LifeState == EntityLifeState.Alive && field.Touches(walker, electrode));

    /// <summary>Kills each grunt that has walked onto an electrode, and the electrode with it.</summary>
    /// <param name="field">The field the entities are on.</param>
    private static void ResolveGrunts(PlayField field)
    {
        foreach (Grunt grunt in field.Entities.Grunts)
        {
            if (grunt.LifeState != EntityLifeState.Alive || FindTouchedElectrode(field, grunt) is not { } electrode)
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
    private static void ResolveHulks(PlayField field)
    {
        foreach (Hulk hulk in field.Entities.Hulks)
        {
            if (FindTouchedElectrode(field, hulk) is { } electrode)
            {
                electrode.Kill();
                field.PlaySoundFrom(SoundTables.PostKill, electrode.Bounds);
            }
        }
    }
}
