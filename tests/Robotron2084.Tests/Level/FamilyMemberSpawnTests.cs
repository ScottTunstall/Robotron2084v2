using Microsoft.Xna.Framework;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     The family members' start of wave (notes §88): they are placed OFF the electrodes, and they
///     begin walking straight away — the ROM's `HUMSTV` staggers each member 1..8 units and the
///     `HUMAN` process is the one robot routine with no STATUS gate, so the family walks while the
///     robots are held off until the game goes live at the start of the wave.
/// </summary>
public sealed class FamilyMemberSpawnTests
{
    private static readonly TimeSpan OneTickSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, OneTickSpan);
    }

    /// <summary>The ROM's own wave table, so the family, electrodes and robots are the arcade's.</summary>
    private static PlayField CreateField(int seed, int wave)
    {
        return new PlayFieldBuilder()
            .WithParameters(WaveParameters.CreateFromWave(wave, WaveTable.GetParameters(wave)))
            .WithRandom(new Random(seed)).Build();
    }

    [Fact]
    public void TheFamilyNeverStartsOnAnElectrode()
    {
        // A family member refuses to step into a live electrode, so one placed inside stands there for
        // the whole wave (the author's report). The placement predicate has to hold for every
        // wave — including the fallback paths, which used to return an unchecked point.
        foreach (var wave in new[] { 1, 5, 6, 7, 12, 20, 33 })
            for (var seed = 1; seed <= 120; seed++)
            {
                var playField = CreateField(seed, wave);

                foreach (var familyMember in playField.FieldEntities.FamilyList.Members)
                foreach (var electrode in playField.FieldEntities.Electrodes)
                    Assert.False(
                        electrode.GetBounds().Intersects(familyMember.GetBounds()),
                        $"wave {wave} seed {seed}: {familyMember.Kind} at {familyMember.GetBounds()} spawns on an "
                        + $"electrode at {electrode.GetBounds()}");
            }
    }

    [Fact]
    public void TheFamilyAlsoNeverWalksOntoAnElectrode()
    {
        // The walk's own obstacle rule, checked over a whole wave: a family member may stand NEXT to an
        // electrode, never inside one.
        var playField = CreateField(9, 7);

        for (var tick = 0; tick < 1800; tick++)
        {
            playField.Update(OneTick());

            foreach (var familyMember in playField.FieldEntities.FamilyList.Members)
            {
                if (!familyMember.IsAlive() || familyMember.IsBeingReprogrammed) continue;

                foreach (var electrode in playField.FieldEntities.Electrodes)
                {
                    if (!electrode.IsAlive()) continue;

                    Assert.False(
                        electrode.GetBounds().Intersects(familyMember.GetBounds()),
                        $"tick {tick}: a {familyMember.Kind} walked into an electrode");
                }
            }
        }
    }

    [Fact]
    public void TheFamilyStartsWalkingWhileTheRobotsAreStillHeld()
    {
        // Two things were holding the family at the start of a wave: the shared
        // `RobotsFrozen()` (which the ROM's robots check but the family members do not), and a whole extra
        // step period seeded into the walk clock — `HUMSTV`'s stagger IS the first wait. Within
        // half a second the family must be moving even though the robots are not.
        var playField = CreateField(3, 7);

        var familyMembers = playField.FieldEntities.FamilyList.Members.ToList();
        Assert.NotEmpty(familyMembers);
        var start = familyMembers.ToDictionary(h => h, h => h.Position);

        for (var tick = 0; tick < 30; tick++) playField.Update(OneTick());

        Assert.True(playField.RobotsFrozen(), "the start of the wave must still be holding the robots");

        var moved = familyMembers.Count(h => h.IsAlive() && h.Position != start[h]);
        Assert.True(moved > 0, "the family was still standing after half a second");
    }
}
