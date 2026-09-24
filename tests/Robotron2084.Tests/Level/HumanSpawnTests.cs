using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
/// The human family's start of wave (notes §88): they are placed OFF the electrodes, and they
/// begin walking straight away — the ROM's `HUMSTV` staggers each member 1..8 units and the
/// `HUMAN` process is the one robot routine with no STATUS gate, so the family walks while the
/// robots are held off for the wave-start appear and while the player is in the start grace.
/// </summary>
public sealed class HumanSpawnTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    /// <summary>The ROM's own wave table, so the family, electrodes and robots are the arcade's.</summary>
    private static PlayField CreateField(int seed, int level) =>
        new(TestSprites.Shared, 
            LevelParameters.FromWave(level, WaveTable.ForWave(level)),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(seed),
            startingLives: 3);

    [Fact]
    public void TheFamilyNeverStartsOnAnElectrode()
    {
        // A human refuses to step into a live electrode, so one placed inside stands there for
        // the whole wave (the author's report). The placement predicate has to hold for every
        // wave — including the fallback paths, which used to return an unchecked point.
        foreach (int level in new[] { 1, 5, 6, 7, 12, 20, 33 })
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                PlayField field = CreateField(seed, level);

                foreach (Human human in field.Humans)
                {
                    foreach (Electrode electrode in field.Electrodes)
                    {
                        Assert.False(
                            electrode.Bounds.Overlaps(human.Bounds),
                            $"level {level} seed {seed}: {human.Kind} at {human.Bounds} spawns on an "
                            + $"electrode at {electrode.Bounds}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheFamilyAlsoNeverWalksOntoAnElectrode()
    {
        // The walk's own obstacle rule, checked over a whole wave: a human may stand NEXT to an
        // electrode, never inside one.
        PlayField field = CreateField(9, 7);

        for (int tick = 0; tick < 1800; tick++)
        {
            field.Update(Frame());

            foreach (Human human in field.Humans)
            {
                if (human.LifeState != EntityLifeState.Alive || human.IsBeingReprogrammed)
                {
                    continue;
                }

                foreach (Electrode electrode in field.Electrodes)
                {
                    if (electrode.LifeState != EntityLifeState.Alive)
                    {
                        continue;
                    }

                    Assert.False(
                        electrode.Bounds.Overlaps(human.Bounds),
                        $"tick {tick}: a {human.Kind} walked into an electrode");
                }
            }
        }
    }

    [Fact]
    public void TheFamilyStartsWalkingWhileTheRobotsAreStillHeld()
    {
        // Two things were holding the family at the start of a wave: the shared
        // `RobotsFrozen` (which the ROM's robots check but the humans do not), and a whole extra
        // step period seeded into the walk clock — `HUMSTV`'s stagger IS the first wait. Within
        // half a second the family must be moving even though the robots are not.
        PlayField field = CreateField(3, 7);

        List<Human> humans = field.Humans.ToList();
        Assert.NotEmpty(humans);
        var start = humans.ToDictionary(h => h, h => h.Position);

        for (int tick = 0; tick < 30; tick++)
        {
            field.Update(Frame());
        }

        Assert.True(field.RobotsFrozen, "the player's start grace must still be holding the robots");

        int moved = humans.Count(h => h.LifeState == EntityLifeState.Alive && h.Position != start[h]);
        Assert.True(moved > 0, "the family was still standing after half a second");
    }
}