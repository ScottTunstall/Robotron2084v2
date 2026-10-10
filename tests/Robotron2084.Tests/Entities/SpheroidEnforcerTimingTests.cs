using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     R5 spheroid/enforcer tempo (notes §17/§29, playtest 2026-09-12
///     "spheroids spawn enforcers WAY too fast" / 2026-09-13 "screen full of
///     sparks"):
///     - spheroid drop countdown decrements once per full 8-frame animation
///     cycle (16 ROM vblanks per step; the 2-vblank beat reaches the DEC only
///     on the last-frame pass) and the randoms are RND(1..A), never 0;
///     - spheroids always drop 1..5 enforcers (ENFNUM roll, ceil(v/2));
///     - enforcers have a 40-ROM-tick grow-up (immobile), a 3-tick AI pass with
///     re-aim (RND(1..31)) and fire (RND(1..ENSTIM)) countdowns, and swoop at
///     ~1.5 arcade pixels/tick toward a 32x32 zone down-right of the player.
///     All tests are seeded — the assertions pin the deterministic ROM contract.
/// </summary>
public sealed class SpheroidEnforcerTimingTests
{
    /// <summary>
    ///     One port tick at 60 ticks/s in INTEGER ticks (no float drift).
    /// </summary>
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame()
    {
        return new GameTime(TimeSpan.Zero, FrameSpan);
    }

    private static PlayField CreateField(int seed, int spheroids = 0, int enfnum = 10, int cdpTim = 30)
    {
        return new PlayFieldBuilder().WithParameters(new LevelParameters(
            1,
            SpheroidCount: spheroids,
            MaxDropsX2: enfnum,
            SpheroidDropDelay: cdpTim)).WithRandom(new Random(seed)).Build();
    }

    [Fact]
    public void Spheroid_DropCount_IsAlwaysBetweenOneAndFive()
    {
        // Wave 1: ENFNUM = 10 → RND(1..10) halved (rounded up) = 1..5, never 0.
        var observed = new HashSet<int>();

        for (var seed = 1; seed <= 16; seed++)
        {
            var field = CreateField(seed, 1);
            var spheroid = field.Entities.Spheroids[0];

            // Run through the start of the wave, then drive the spheroid standalone:
            // its drops land in the field (field.EnforcerCount) but the
            // dropped enforcers are never updated, so they cannot kill the
            // standing player and freeze the simulation.
            for (var tick = 1; tick <= WaveStartTicks.UntilLive(field); tick++) field.Update(Frame());

            for (var tick = 0; tick < 2000 && !spheroid.IsDead(); tick++) spheroid.Update(Frame(), field);

            Assert.Equal(EntityLifeState.Dead, spheroid.LifeState);
            var dropped = field.Entities.Enforcers.GetLiveCount();
            Assert.InRange(dropped, 1, 5);
            observed.Add(dropped);
        }

        Assert.True(observed.Count >= 2, $"drop count was constant {observed.First()} across 16 seeds");
    }

    [Fact]
    public void Spheroid_NeverDropsBeforeTheMinimumBouncePhase()
    {
        // Minimum initial countdown = RND(1..CDPTIM) = 1 step, and a step is now a
        // full FIVE-animation-frame wrap (CIRCLE advances `OPICT += 4` per `NAP 2` beat and
        // wraps at CIRP4, notes §90): 5 x 3 = 15 fiftieths of a second = 18 port ticks. (It was
        // 16 frames while §56.2 mis-read the boundary as CIRP3.) The start of the wave
        // holds everything until the game goes live, so the earliest possible drop is
        // 18 ticks after that; no drop through the 17th for every seed pins that floor.
        for (var seed = 1; seed <= 8; seed++)
        {
            var field = CreateField(seed, 1);

            for (var tick = 1; tick <= WaveStartTicks.UntilLive(field) + 17; tick++)
            {
                field.Update(Frame());
                Assert.True(field.Entities.Enforcers.GetLiveCount() == 0, $"seed {seed}: drop at tick {tick}");
            }
        }
    }

    [Fact]
    public void Enforcer_IsImmobileAndSilentDuringGrowUp()
    {
        var field = CreateField(7);
        Enforcer enforcer = new(TestSprites.Shared,
            new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(30), ScreenSize.ToPortPixelsFromArcadePixels(30)),
            new Random(42), 30);
        var start = enforcer.Position;

        // Run through the start of the wave so RobotsFrozen() is false for the enforcer.
        for (var tick = 1; tick <= WaveStartTicks.UntilLive(field); tick++) field.Update(Frame());

        // The ROM grow-up is 45 frames = 54 port ticks (notes §65; the old model said
        // 40 frames, which is why this used to stop at PortTicks(40) = 48). The last
        // of those ticks is the one ENFR10 runs on, so immobility covers 1..53.
        for (var tick = 1; tick < ArcadeClock.ToPortTicksRoundedUp(EnforcerTuning.GrowUpRomFrames); tick++)
        {
            enforcer.Update(Frame(), field);
            Assert.True(enforcer.Position == start, $"moved during grow-up at tick {tick}");
            Assert.True(field.GetActiveSparkCount() == 0, $"fired during grow-up at tick {tick}");
        }

        for (var tick = 1; tick <= 160 && enforcer.Position == start; tick++) enforcer.Update(Frame(), field);

        Assert.True(enforcer.Position != start, "enforcer never left the grow-up spot");
    }

    [Fact]
    public void Enforcer_GrowFrames_ChangeOnTheRomsNineFrameBoundaries()
    {
        // FIVE grow animation frames over 45 fiftieths of a second = 9 frames each, on the clock-unit
        // clock, so animation frame n starts on the first tick where 5t >= 9n x 6 — i.e.
        // ticks 1, 11, 22, 33, 44. The old PortTicks(9) = 10 switched every 10 ticks
        // and ran out early inside a correctly-timed growth (notes §65.3).
        var field = CreateField(7);
        Enforcer enforcer = new(TestSprites.Shared,
            new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(30), ScreenSize.ToPortPixelsFromArcadePixels(30)),
            new Random(42), 30);

        for (var tick = 1; tick <= WaveStartTicks.UntilLive(field); tick++) field.Update(Frame());

        int[] expectedStarts = { 1, 11, 22, 33, 44 };
        var growTicks = ArcadeClock.ToPortTicksRoundedUp(EnforcerTuning.GrowUpRomFrames); // 54

        for (var tick = 1; tick < growTicks; tick++)
        {
            enforcer.Update(Frame(), field);
            var expected = Array.FindLastIndex(expectedStarts, s => s <= tick);
            Assert.Equal(expected, enforcer.GetGrowAnimationFrameIndex());
        }

        enforcer.Update(Frame(), field); // tick 54: the grow-up ends, ENFR10 runs
        Assert.Equal(-1, enforcer.GetGrowAnimationFrameIndex());
    }

    [Fact]
    public void Enforcer_FireGaps_MatchTheRomRate()
    {
        // Fire countdown = RND(1..ENSTIM) BEATS, a beat being NAP 3 + 1 = 4 ROM
        // frames (notes §43/§55), so with ENSTIM = 30 every gap sits in
        // [PortTicks(4), PortTicks(120)] = [4, 144] port ticks (allow a tick of
        // slack for the 6/5 beat accumulator), and (seeded, deterministic) at least
        // one gap must exceed the old model's maximum of PortTicks(30) = 36.
        var field = CreateField(11);
        Enforcer enforcer = new(TestSprites.Shared,
            new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(30), ScreenSize.ToPortPixelsFromArcadePixels(30)),
            new Random(1234), 30);

        for (var tick = 1; tick <= WaveStartTicks.UntilLive(field); tick++) field.Update(Frame());

        var seen = new HashSet<Spark>();
        var fireTicks = new List<int>();

        for (var tick = 1; tick <= 3000 && fireTicks.Count < 10; tick++)
        {
            enforcer.Update(Frame(), field);
            foreach (var spark in field.Entities.Sparks)
                if (seen.Add(spark) && spark.IsAlive() && fireTicks.Count < 10)
                    fireTicks.Add(tick);
        }

        Assert.True(fireTicks.Count >= 4, $"only {fireTicks.Count} sparks observed in 3000 ticks");
        for (var i = 1; i < fireTicks.Count; i++)
        {
            var gap = fireTicks[i] - fireTicks[i - 1];
            Assert.InRange(
                gap,
                ArcadeClock.ToPortTicks(EnforcerTuning.BeatIntervalRomFrames) - 1,
                ArcadeClock.ToPortTicks(EnforcerTuning.BeatIntervalRomFrames * 30) + 1);
        }

        var maxGap = fireTicks.Zip(fireTicks.Skip(1), (a, b) => b - a).Max();
        Assert.True(maxGap > ArcadeClock.ToPortTicks(30), $"max gap {maxGap} never exceeded the pre-fix maximum");
    }

    [Fact]
    public void Enforcer_LoiterCirclesNearThePlayer()
    {
        // The destination zone is a 32x32 arcade pixel (64 port pixel) box
        // down-right of the player; over a long run the enforcer must
        // spend time close to the player, not wander the whole field.
        var field = CreateField(11);
        Enforcer enforcer = new(TestSprites.Shared,
            new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(30), ScreenSize.ToPortPixelsFromArcadePixels(30)),
            new Random(1234), 30);

        for (var tick = 1; tick <= WaveStartTicks.UntilLive(field); tick++) field.Update(Frame());

        var player = field.Player.Position;

        var closest = int.MaxValue;
        for (var tick = 1; tick <= 3000; tick++)
        {
            enforcer.Update(Frame(), field);
            closest = Math.Min(closest, (int)IntVector2.ComputeDistanceSquared(enforcer.Position, player));
        }

        var reach = ScreenSize.ToPortPixelsFromArcadePixels(65);
        Assert.True(closest < reach * reach,
            $"enforcer never came within {reach} port px of the player (closest^2 = {closest})");
    }
}
