using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>Works out what is left on a field when the player dies, so the next life starts with only that.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>PLEND</c> (from <c>PLEND1</c>), <c>PLSAV</c> and <c>PLRES</c></item>
/// <item>Disassembly: the player-death routine and the player save and restore routines it calls</item>
/// </list>
/// A robot or a family member that was killed, or rescued, stays gone. What is left is counted as the arcade counts it: a thing
/// that has begun to die is already off the count, and a human in a brain's hold has already gone. Enforcers do not carry over; they
/// turn back into spheroids, one for every four, and at least one when there are enforcers but no spheroids, never more than
/// the wave began with. The speed floor goes back to the wave's own, and the grunts keep the speed limit they had reached,
/// raised to that floor if it is lower (notes §134).
/// </remarks>
public static class WaveSurvivors
{
    /// <summary>How many enforcers make one spheroid.</summary>
    private const int EnforcersPerSpheroid = 4;

    /// <summary>Counts what is left on a field.</summary>
    /// <param name="field">The field the player has died on.</param>
    /// <returns>The wave's parameters with the counts of what is left, to start the next life with.</returns>
    public static LevelParameters GetFrom(PlayField field)
    {
        FieldEntities entities = field.Entities;
        LevelParameters wave = field.Parameters;
        int gruntLimit = entities.Grunts.Where(grunt => grunt.IsAlive()).Select(grunt => grunt.MoveDelayBeats)
            .DefaultIfEmpty(wave.GruntMoveDelay).Min();

        return wave with
        {
            GruntCount = CountAlive(entities.Grunts),
            BerzerkRobotCount = CountAlive(entities.BerzerkRobots),
            GorfCount = CountAlive(entities.Gorfs),
            ElectrodeCount = CountAlive(entities.Electrodes),
            MikeyCount = CountFamily(entities, HumanKind.Mikey),
            MommyCount = CountFamily(entities, HumanKind.Mommy),
            DaddyCount = CountFamily(entities, HumanKind.Daddy),
            HulkCount = CountAlive(entities.Hulks),
            BrainCount = CountAlive(entities.Brains),
            SpheroidCount = CountSpheroidsAfterConverting(CountAlive(entities.Enforcers), CountAlive(entities.Spheroids), wave.SpheroidCount),
            QuarkCount = CountAlive(entities.Quarks),
            TankCount = CountAlive(entities.Tanks),
            GruntMoveDelay = Math.Max(gruntLimit, wave.GruntSpeedFloor),
        };
    }

    /// <summary>Works out how many spheroids there are once the leftover enforcers have turned back into them.</summary>
    /// <param name="enforcers">The enforcers left.</param>
    /// <param name="spheroids">The spheroids left.</param>
    /// <param name="startedWith">How many spheroids the life began with, which is the most there can be.</param>
    private static int CountSpheroidsAfterConverting(int enforcers, int spheroids, int startedWith)
    {
        if (enforcers == 0)
        {
            return spheroids;
        }

        int made = enforcers / EnforcersPerSpheroid;
        if (made == 0 && spheroids == 0)
        {
            made = 1;
        }

        return Math.Min(made + spheroids, startedWith);
    }

    private static int CountAlive<T>(EntityList<T> list)
        where T : class, IEntity => list.Count(entity => entity.IsAlive());

    private static int CountFamily(FieldEntities entities, HumanKind kind) =>
        entities.Family.Members.Count(human => human.Kind == kind && human.IsGraspable());
}
