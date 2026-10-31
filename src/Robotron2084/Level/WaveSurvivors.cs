using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>Works out what is left on a field when the player dies, so the next life starts with only that.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>PLEND</c> (from <c>PLEND1</c>),
///             <c>PLSAV</c> and <c>PLRES</c>
///         </item>
///         <item>Disassembly: the player-death routine and the player save and restore routines it calls</item>
///     </list>
///     A robot or a family member that was killed, or rescued, stays gone. What is left is
///     counted as the arcade counts it: a thing that has begun to die is already off the count, and a
///     human in a brain's hold has already gone. Enforcers do not carry over; they turn back into
///     spheroids, one for every four, and at least one when there are enforcers but no spheroids, never
///     more than the wave began with. The speed floor goes back to the wave's own, and the grunts keep
///     the speed limit they had reached, raised to that floor if it is lower (notes §134).</item>
///     </list>
/// </remarks>
public static class WaveSurvivors
{
    /// <summary>How many enforcers make one spheroid.</summary>
    private const int EnforcersPerSpheroid = 4;

    /// <summary>Counts what is left on a field.</summary>
    /// <param name="playField">The field the player has died on.</param>
    /// <returns>The wave's parameters with the counts of what is left, to start the next life with.</returns>
    public static WaveParameters GetFrom(PlayField playField)
    {
        var fieldEntities = playField.FieldEntities;
        var waveParameters = playField.WaveParameters;
        var gruntLimit = fieldEntities.Grunts.Where(grunt => grunt.IsAlive()).Select(grunt => grunt.MoveDelayBeats)
            .DefaultIfEmpty(waveParameters.GruntMoveDelay).Min();

        return waveParameters with
        {
            GruntCount = CountAlive(fieldEntities.Grunts),
            BerzerkRobotCount = CountAlive(fieldEntities.BerzerkRobots),
            GorfCount = CountAlive(fieldEntities.Gorfs),
            ElectrodeCount = CountAlive(fieldEntities.Electrodes),
            MikeyCount = CountFamilyMembers(fieldEntities, HumanKind.Mikey),
            MommyCount = CountFamilyMembers(fieldEntities, HumanKind.Mommy),
            DaddyCount = CountFamilyMembers(fieldEntities, HumanKind.Daddy),
            HulkCount = CountAlive(fieldEntities.Hulks),
            BrainCount = CountAlive(fieldEntities.Brains),
            SpheroidCount = CountSpheroidsAfterConverting(CountAlive(fieldEntities.Enforcers),
                CountAlive(fieldEntities.Spheroids), waveParameters.SpheroidCount),
            QuarkCount = CountAlive(fieldEntities.Quarks),
            TankCount = CountAlive(fieldEntities.Tanks),
            GruntMoveDelay = Math.Max(gruntLimit, waveParameters.GruntSpeedFloor)
        };
    }

    /// <summary>Counts the things in a list that are still alive.</summary>
    /// <param name="list">The list to count.</param>
    private static int CountAlive<T>(EntityList<T> list)
        where T : class, IEntity
    {
        return list.Count(entity => entity.IsAlive());
    }

    /// <summary>Counts the members of one kind of family member who are standing on the field and free.</summary>
    /// <param name="fieldEntities">What is on the field.</param>
    /// <param name="kind">Which family member to count.</param>
    private static int CountFamilyMembers(FieldEntities fieldEntities, HumanKind kind)
    {
        return fieldEntities.FamilyList.Members.Count(human => human.Kind == kind && human.IsGraspable());
    }

    /// <summary>Works out how many spheroids there are once the leftover enforcers have turned back into them.</summary>
    /// <param name="enforcers">The enforcers left.</param>
    /// <param name="spheroids">The spheroids left.</param>
    /// <param name="startedWith">How many spheroids the life began with, which is the most there can be.</param>
    private static int CountSpheroidsAfterConverting(int enforcers, int spheroids, int startedWith)
    {
        if (enforcers == 0) return spheroids;

        var spheroidsFromEnforcers = enforcers / EnforcersPerSpheroid;
        if (spheroidsFromEnforcers == 0 && spheroids == 0) spheroidsFromEnforcers = 1;

        return Math.Min(spheroidsFromEnforcers + spheroids, startedWith);
    }
}
