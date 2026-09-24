using Microsoft.Xna.Framework;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The spheroid's PICTURE CHAIN (notes §56.2 as corrected by §90) — `OPICT += 4` once
/// per beat (`NAP 2` = 3 ROM frames = 3.6 port ticks), where the wrap boundary decides
/// how many pictures a phase has:
/// <list type="bullet">
/// <item>`CIRCLE` (`ANIMATE_SPHEROID`) and `CIRC3L` both compare against `CIRP4`
/// (`CMPD #$1502`), so the idle spin and the ESCAPE cycle FIVE pictures, CIRP0..CIRP4;</item>
/// <item>`CIRC2L` compares against `CIRP7` (`CMPD #$150E`), so the drop phase cycles
/// all eight.</item>
/// </list>
/// The escape used to advance its picture on every port TICK and only ever showed
/// CIRP0..CIRP3 — 3.6x the arcade's rate, and a picture short. That is the author's
/// *"the spheroid, after giving birth to all the enforcers, looks weird animation
/// wise"* (2026-09-17). A spheroid is also BORN on CIRP4 (MPROB stores the `MKPROB`
/// picture argument in OPICT), not on the dot.
/// </summary>
public sealed class SpheroidAnimationTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    /// <summary>Frames of field.Update needed to expire the player's 2-second start grace.</summary>
    private const int GraceWarmupTicks = 121;

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    /// <summary>
    /// ENFNUM 2 + CDPTIM 3: the initial countdown is 1..3 five-picture wraps, the drop
    /// phase's is always 1 (CDPTIM/4 = 0), and `ceil(RND(1..2)/2)` = 1 enforcer — so the
    /// first drop ends the drop phase and the spheroid escapes almost at once.
    /// </summary>
    private static PlayField CreateField(int seed) =>
        new(TestSprites.Shared, 
            new LevelParameters(
                LevelNumber: 1,
                SpheroidCount: 1,
                MaxDropsX2: 2,
                SpheroidDropDelay: 3),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(seed),
            startingLives: 3);

    /// <summary>
    /// Drives one spheroid (seeded, so this is deterministic) through the start grace
    /// and on into its escape phase. Dropped enforcers are never updated, so they cannot
    /// kill the standing player and freeze the field.
    /// </summary>
    private static (PlayField Field, Spheroid Spheroid) DriveToEscape(int seed)
    {
        PlayField field = CreateField(seed);
        Spheroid spheroid = field.Spheroids[0];

        for (int tick = 1; tick <= GraceWarmupTicks; tick++)
        {
            field.Update(Frame());
        }

        for (int guard = 0; guard < 4000 && !spheroid.IsEscaping; guard++)
        {
            spheroid.Update(Frame(), field);
        }

        Assert.True(spheroid.IsEscaping, "the spheroid never reached its escape phase");
        return (field, spheroid);
    }

    [Fact]
    public void TheEscapeSpinsTheIdleFivePictures_OnePicturePerBeat()
    {
        // How long the escape lasts depends on where the seeded bounce leaves the
        // spheroid, so several seeds are tried (deterministically, in order) and the
        // first that runs at least a full five-picture cycle is the one asserted on.
        // Every observed change is checked for EVERY seed, so a fast strobe cannot
        // hide in the short escapes.
        for (int seed = 1; seed <= 40; seed++)
        {
            (PlayField field, Spheroid spheroid) = DriveToEscape(seed);

            int previous = spheroid.PictureIndex;
            int lastChangeTick = 0;
            int changes = 0;
            var seen = new HashSet<int>();

            for (int tick = 1; tick <= 600 && spheroid.LifeState != EntityLifeState.Dead; tick++)
            {
                spheroid.Update(Frame(), field);

                if (spheroid.PictureIndex == previous)
                {
                    continue;
                }

                // One picture per `NAP 2` beat: 3.6 port ticks each, which the
                // exact-6ths clock lands 3 and 4 ticks apart. The per-tick strobe was 1.
                Assert.True(
                    tick - lastChangeTick >= 3,
                    $"seed {seed}: picture changed {tick - lastChangeTick} tick(s) after the last one");

                // The chain only ever advances one entry (`ADDD #4`) or wraps to CIRP0.
                Assert.True(
                    spheroid.PictureIndex == 0 || spheroid.PictureIndex == previous + 1,
                    $"seed {seed}: picture jumped {previous} -> {spheroid.PictureIndex}");

                // CIRC3L wraps at CIRP4, so the escape shows 0..4 and NEVER a
                // drop-phase picture.
                Assert.InRange(spheroid.PictureIndex, 0, 4);

                lastChangeTick = tick;
                previous = spheroid.PictureIndex;
                changes++;
                seen.Add(previous);
            }

            if (changes >= 5)
            {
                // CIRP4 — the frame the port never showed — is part of the cycle.
                Assert.Contains(4, seen);
                return;
            }
        }

        Assert.Fail("no seed produced a full five-picture escape cycle to judge");
    }

    [Fact]
    public void TheSpinPhaseNeverShowsADropPicture()
    {
        // The same drive, watching the phases as they go: the idle spin (CIRCLE) is
        // limited to CIRP0..CIRP4, and CIRP5 is the first picture only the drop phase
        // (CIRC2L) can show — which is also how this test tells the phases apart.
        PlayField field = CreateField(seed: 3);
        Spheroid spheroid = field.Spheroids[0];

        var spinPictures = new HashSet<int>();
        var dropPictures = new HashSet<int>();
        bool dropping = false;

        for (int tick = 1; tick <= 4000 && !spheroid.IsEscaping; tick++)
        {
            if (tick <= GraceWarmupTicks)
            {
                field.Update(Frame()); // let the start grace expire
            }
            else
            {
                spheroid.Update(Frame(), field);
            }

            if (spheroid.PictureIndex > 4)
            {
                dropping = true;
            }

            (dropping ? dropPictures : spinPictures).Add(spheroid.PictureIndex);
        }

        Assert.NotEmpty(spinPictures);
        Assert.All(spinPictures, index => Assert.InRange(index, 0, 4));
        Assert.Contains(5, dropPictures);
        Assert.Contains(7, dropPictures);
    }

    [Fact]
    public void ASpheroidIsBornOnCIRP4()
    {
        // MPROB: `LDD ,U++ / STD OLDPIC,X / STD OPICT,X` — the picture argument of
        // `MKPROB CIRCLE,CIRP4,CIRKIL` becomes the new object's OPICT, so the very
        // first picture on screen is the medium ring, not the dot.
        PlayField field = CreateField(seed: 11);

        Assert.Equal(4, field.Spheroids[0].PictureIndex);
    }
}