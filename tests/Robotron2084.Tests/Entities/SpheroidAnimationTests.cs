using Microsoft.Xna.Framework;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     The spheroid's sprite CHAIN (notes §56.2 as corrected by §90) — `OPICT += 4` once
///     per beat (`NAP 2` = 3 fiftieths of a second = 3.6 port ticks), where the wrap boundary decides
///     how many animation frames a phase has:
///     <list type="bullet">
///         <item>
///             `CIRCLE` (`ANIMATE_SPHEROID`) and `CIRC3L` both compare against `CIRP4`
///             (`CMPD #$1502`), so the idle spin and the ESCAPE cycle FIVE animation frames, CIRP0..CIRP4;
///         </item>
///         <item>
///             `CIRC2L` compares against `CIRP7` (`CMPD #$150E`), so the drop phase cycles
///             all eight.
///         </item>
///     </list>
///     The escape used to advance its animation frame on every port TICK and only ever showed
///     CIRP0..CIRP3 — 3.6x the arcade's rate, and an animation frame short. That is the author's
///     *"the spheroid, after giving birth to all the enforcers, looks weird animation
///     wise"* (2026-09-17). A spheroid is also BORN on CIRP4 (MPROB stores the `MKPROB`
///     animation frame argument in OPICT), not on the dot.
/// </summary>
public sealed class SpheroidAnimationTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame()
    {
        return new GameTime(TimeSpan.Zero, FrameSpan);
    }

    /// <summary>
    ///     ENFNUM 2 + CDPTIM 3: the initial countdown is 1..3 five-animation-frame wraps, the drop
    ///     phase's is always 1 (CDPTIM/4 = 0), and `ceil(RND(1..2)/2)` = 1 enforcer — so the
    ///     first drop ends the drop phase and the spheroid escapes almost at once.
    /// </summary>
    private static PlayField CreateField(int seed)
    {
        return new PlayFieldBuilder().WithParameters(new LevelParameters(
            1,
            SpheroidCount: 1,
            MaxDropsX2: 2,
            SpheroidDropDelay: 3)).WithRandom(new Random(seed)).Build();
    }

    /// <summary>
    ///     Drives one spheroid (seeded, so this is deterministic) through the start of the wave
    ///     and on into its escape phase. Dropped enforcers are never updated, so they cannot
    ///     kill the standing player and freeze the field.
    /// </summary>
    private static (PlayField Field, Spheroid Spheroid) DriveToEscape(int seed)
    {
        var field = CreateField(seed);
        var spheroid = field.Entities.Spheroids[0];

        for (var tick = 1; tick <= WaveStartTicks.UntilLive(field); tick++) field.Update(Frame());

        for (var guard = 0; guard < 4000 && !spheroid.IsEscaping; guard++) spheroid.Update(Frame(), field);

        Assert.True(spheroid.IsEscaping, "the spheroid never reached its escape phase");
        return (field, spheroid);
    }

    [Fact]
    public void TheEscapeSpinsTheIdleFiveAnimationFrames_OneAnimationFramePerBeat()
    {
        // How long the escape lasts depends on where the seeded bounce leaves the
        // spheroid, so several seeds are tried (deterministically, in order) and the
        // first that runs at least a full five-animation-frame cycle is the one asserted on.
        // Every observed change is checked for EVERY seed, so a fast strobe cannot
        // hide in the short escapes.
        for (var seed = 1; seed <= 40; seed++)
        {
            var (field, spheroid) = DriveToEscape(seed);

            var previous = spheroid.AnimationFrameIndex;
            var lastChangeTick = 0;
            var changes = 0;
            var seen = new HashSet<int>();

            for (var tick = 1; tick <= 600 && !spheroid.IsDead(); tick++)
            {
                spheroid.Update(Frame(), field);

                if (spheroid.AnimationFrameIndex == previous) continue;

                // One animation frame per `NAP 2` beat: 3.6 port ticks each, which the
                // clock-unit clock lands 3 and 4 ticks apart. The per-tick strobe was 1.
                Assert.True(
                    tick - lastChangeTick >= 3,
                    $"seed {seed}: animation frame changed {tick - lastChangeTick} tick(s) after the last one");

                // The chain only ever advances one entry (`ADDD #4`) or wraps to CIRP0.
                Assert.True(
                    spheroid.AnimationFrameIndex == 0 || spheroid.AnimationFrameIndex == previous + 1,
                    $"seed {seed}: animation frame jumped {previous} -> {spheroid.AnimationFrameIndex}");

                // CIRC3L wraps at CIRP4, so the escape shows 0..4 and NEVER a
                // drop-phase animation frame.
                Assert.InRange(spheroid.AnimationFrameIndex, 0, 4);

                lastChangeTick = tick;
                previous = spheroid.AnimationFrameIndex;
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

        Assert.Fail("no seed produced a full five-animation-frame escape cycle to judge");
    }

    [Fact]
    public void TheSpinPhaseNeverShowsADropAnimationFrame()
    {
        // The same drive, watching the phases as they go: the idle spin (CIRCLE) is
        // limited to CIRP0..CIRP4, and CIRP5 is the first animation frame only the drop phase
        // (CIRC2L) can show — which is also how this test tells the phases apart.
        var field = CreateField(3);
        var spheroid = field.Entities.Spheroids[0];

        var spinAnimationFrames = new HashSet<int>();
        var dropAnimationFrames = new HashSet<int>();
        var dropping = false;

        for (var tick = 1; tick <= 4000 && !spheroid.IsEscaping; tick++)
        {
            if (tick <= WaveStartTicks.UntilLive(field))
                field.Update(Frame()); // run through the start of the wave
            else
                spheroid.Update(Frame(), field);

            if (spheroid.AnimationFrameIndex > 4) dropping = true;

            (dropping ? dropAnimationFrames : spinAnimationFrames).Add(spheroid.AnimationFrameIndex);
        }

        Assert.NotEmpty(spinAnimationFrames);
        Assert.All(spinAnimationFrames, index => Assert.InRange(index, 0, 4));
        Assert.Contains(5, dropAnimationFrames);
        Assert.Contains(7, dropAnimationFrames);
    }

    [Fact]
    public void ASpheroidIsBornOnCIRP4()
    {
        // MPROB: `LDD ,U++ / STD OLDPIC,X / STD OPICT,X` — the animation frame argument of
        // `MKPROB CIRCLE,CIRP4,CIRKIL` becomes the new object's OPICT, so the very
        // first animation frame on screen is the medium ring, not the dot.
        var field = CreateField(11);

        Assert.Equal(4, field.Entities.Spheroids[0].AnimationFrameIndex);
    }
}
