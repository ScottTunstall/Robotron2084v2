using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// R5 spheroid/enforcer tempo (notes §17/§29, playtest 2026-09-12
/// "spheroids spawn enforcers WAY too fast" / 2026-09-13 "screen full of
/// sparks"):
/// - spheroid drop countdown decrements once per full 8-frame animation
///   cycle (16 ROM vblanks per step; the 2-vblank beat reaches the DEC only
///   on the last-frame pass) and the randoms are RND(1..A), never 0;
/// - spheroids always drop 1..5 enforcers (ENFNUM roll, ceil(v/2));
/// - enforcers have a 40-ROM-tick grow-up (immobile), a 3-tick AI pass with
///   re-aim (RND(1..31)) and fire (RND(1..ENSTIM)) countdowns, and swoop at
///   ~1.5 spec px/tick toward a 32x32 zone down-right of the player.
/// All tests are seeded — the assertions pin the deterministic ROM contract.
/// </summary>
public sealed class SpheroidEnforcerTimingTests
{
    /// <summary>
    /// One port tick at 60 ticks/s in INTEGER ticks (no float drift).
    /// TicksPerSecond/60 = 166,666 ticks/frame (truncated), so the player's
    /// 2-second start grace (PlayerStartGraceSeconds, time-based) expires on
    /// the 121st such frame (120 x 166,666 = 1.999992 s; 121 x 166,666 =
    /// 2.0166586 s), which is what unfreezes RobotsFrozen.
    /// </summary>
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    /// <summary>Frames of field.Update needed to expire the start grace period.</summary>
    private const int GraceWarmupTicks = 121;

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int seed, int spheroids = 0, int enfnum = 10, int cdpTim = 30) =>
        new(TestSprites.Shared, 
            new LevelParameters(
                LevelNumber: 1,
                SpheroidCount: spheroids,
                MaxDropsX2: enfnum,
                SpheroidDropDelay: cdpTim),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(seed),
            startingLives: 3);

    [Fact]
    public void Spheroid_DropCount_IsAlwaysBetweenOneAndFive()
    {
        // Wave 1: ENFNUM = 10 → RND(1..10) halved (rounded up) = 1..5, never 0.
        var observed = new HashSet<int>();

        for (int seed = 1; seed <= 16; seed++)
        {
            PlayField field = CreateField(seed, spheroids: 1);
            Spheroid spheroid = field.Spheroids[0];

            // Expire the start grace, then drive the spheroid standalone:
            // its drops land in the field (field.EnforcerCount) but the
            // dropped enforcers are never updated, so they cannot kill the
            // standing player and freeze the simulation.
            for (int tick = 1; tick <= GraceWarmupTicks; tick++)
            {
                field.Update(Frame());
            }

            for (int tick = 0; tick < 2000 && spheroid.LifeState != EntityLifeState.Dead; tick++)
            {
                spheroid.Update(Frame(), field);
            }

            Assert.Equal(EntityLifeState.Dead, spheroid.LifeState);
            int dropped = field.EnforcerCount;
            Assert.InRange(dropped, 1, 5);
            observed.Add(dropped);
        }

        Assert.True(observed.Count >= 2, $"drop count was constant {observed.First()} across 16 seeds");
    }

    [Fact]
    public void Spheroid_NeverDropsBeforeTheMinimumBouncePhase()
    {
        // Minimum initial countdown = RND(1..CDPTIM) = 1 step, and a step is now a
        // full FIVE-picture wrap (CIRCLE advances `OPICT += 4` per `NAP 2` beat and
        // wraps at CIRP4, notes §90): 5 x 3 = 15 ROM frames = 18 port ticks. (It was
        // 16 frames while §56.2 mis-read the boundary as CIRP3.) The start grace
        // freezes everything until the 121st frame, so the earliest possible drop is
        // around tick 139; no drop through tick 138 for every seed pins that floor.
        for (int seed = 1; seed <= 8; seed++)
        {
            PlayField field = CreateField(seed, spheroids: 1);

            for (int tick = 1; tick <= GraceWarmupTicks + 17; tick++)
            {
                field.Update(Frame());
                Assert.True(field.EnforcerCount == 0, $"seed {seed}: drop at tick {tick}");
            }
        }
    }

    [Fact]
    public void Enforcer_IsImmobileAndSilentDuringGrowUp()
    {
        PlayField field = CreateField(7);
        Enforcer enforcer = new(TestSprites.Shared, new IntVector2(ScreenSize.Scaled(30), ScreenSize.Scaled(30)), new Random(42), fireDelayRomTicks: 30);
        IntVector2 start = enforcer.Position;

        // Expire the start grace so RobotsFrozen is false for the enforcer.
        for (int tick = 1; tick <= GraceWarmupTicks; tick++)
        {
            field.Update(Frame());
        }

        // The ROM grow-up is 45 frames = 54 port ticks (notes §65; the old model said
        // 40 frames, which is why this used to stop at PortTicks(40) = 48). The last
        // of those ticks is the one ENFR10 runs on, so immobility covers 1..53.
        for (int tick = 1; tick < GameplayConstants.PortTicksCeil(GameplayConstants.EnforcerGrowUpRomFrames); tick++)
        {
            enforcer.Update(Frame(), field);
            Assert.True(enforcer.Position == start, $"moved during grow-up at tick {tick}");
            Assert.True(field.ActiveSparkCount == 0, $"fired during grow-up at tick {tick}");
        }

        for (int tick = 1; tick <= 160 && enforcer.Position == start; tick++)
        {
            enforcer.Update(Frame(), field);
        }

        Assert.True(enforcer.Position != start, "enforcer never left the grow-up spot");
    }

    [Fact]
    public void Enforcer_GrowFrames_ChangeOnTheRomsNineFrameBoundaries()
    {
        // FIVE grow pictures over 45 ROM frames = 9 frames each, on the exact-6ths
        // clock, so picture n starts on the first tick where 5t >= 9n x 6 — i.e.
        // ticks 1, 11, 22, 33, 44. The old PortTicks(9) = 10 switched every 10 ticks
        // and ran out early inside a correctly-timed growth (notes §65.3).
        PlayField field = CreateField(7);
        Enforcer enforcer = new(TestSprites.Shared, new IntVector2(ScreenSize.Scaled(30), ScreenSize.Scaled(30)), new Random(42), fireDelayRomTicks: 30);

        for (int tick = 1; tick <= GraceWarmupTicks; tick++)
        {
            field.Update(Frame());
        }

        int[] expectedStarts = { 1, 11, 22, 33, 44 };
        int growTicks = GameplayConstants.PortTicksCeil(GameplayConstants.EnforcerGrowUpRomFrames); // 54

        for (int tick = 1; tick < growTicks; tick++)
        {
            enforcer.Update(Frame(), field);
            int expected = Array.FindLastIndex(expectedStarts, s => s <= tick);
            Assert.Equal(expected, enforcer.GrowFrameIndex);
        }

        enforcer.Update(Frame(), field); // tick 54: the grow-up ends, ENFR10 runs
        Assert.Equal(-1, enforcer.GrowFrameIndex);
    }

    [Fact]
    public void Enforcer_FireGaps_MatchTheRomRate()
    {
        // Fire countdown = RND(1..ENSTIM) BEATS, a beat being NAP 3 + 1 = 4 ROM
        // frames (notes §43/§55), so with ENSTIM = 30 every gap sits in
        // [PortTicks(4), PortTicks(120)] = [4, 144] port ticks (allow a tick of
        // slack for the 6/5 beat accumulator), and (seeded, deterministic) at least
        // one gap must exceed the old model's maximum of PortTicks(30) = 36.
        PlayField field = CreateField(11);
        Enforcer enforcer = new(TestSprites.Shared, new IntVector2(ScreenSize.Scaled(30), ScreenSize.Scaled(30)), new Random(1234), fireDelayRomTicks: 30);

        for (int tick = 1; tick <= GraceWarmupTicks; tick++)
        {
            field.Update(Frame());
        }

        var seen = new HashSet<Spark>();
        var fireTicks = new List<int>();

        for (int tick = 1; tick <= 3000 && fireTicks.Count < 10; tick++)
        {
            enforcer.Update(Frame(), field);
            foreach (Spark spark in field.Sparks)
            {
                if (seen.Add(spark) && spark.LifeState == EntityLifeState.Alive && fireTicks.Count < 10)
                {
                    fireTicks.Add(tick);
                }
            }
        }

        Assert.True(fireTicks.Count >= 4, $"only {fireTicks.Count} sparks observed in 3000 ticks");
        for (int i = 1; i < fireTicks.Count; i++)
        {
            int gap = fireTicks[i] - fireTicks[i - 1];
            Assert.InRange(
                gap,
                GameplayConstants.PortTicks(GameplayConstants.EnforcerBeatRomFrames) - 1,
                GameplayConstants.PortTicks(GameplayConstants.EnforcerBeatRomFrames * 30) + 1);
        }

        int maxGap = fireTicks.Zip(fireTicks.Skip(1), (a, b) => b - a).Max();
        Assert.True(maxGap > GameplayConstants.PortTicks(30), $"max gap {maxGap} never exceeded the pre-fix maximum");
    }

    [Fact]
    public void Enforcer_LoiterCirclesNearThePlayer()
    {
        // The destination zone is a 32x32 spec-px (64 internal px) box
        // down-right of the player; over a long run the enforcer must
        // spend time close to the player, not wander the whole field.
        PlayField field = CreateField(11);
        Enforcer enforcer = new(TestSprites.Shared, new IntVector2(ScreenSize.Scaled(30), ScreenSize.Scaled(30)), new Random(1234), fireDelayRomTicks: 30);

        for (int tick = 1; tick <= GraceWarmupTicks; tick++)
        {
            field.Update(Frame());
        }

        IntVector2 player = field.Player.Position;

        int closest = int.MaxValue;
        for (int tick = 1; tick <= 3000; tick++)
        {
            enforcer.Update(Frame(), field);
            closest = Math.Min(closest, (int)IntVector2.DistanceSquared(enforcer.Position, player));
        }

        int reach = ScreenSize.Scaled(65);
        Assert.True(closest < reach * reach, $"enforcer never came within {reach} port px of the player (closest^2 = {closest})");
    }
}