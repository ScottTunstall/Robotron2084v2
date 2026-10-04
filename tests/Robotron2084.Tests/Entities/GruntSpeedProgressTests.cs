using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Grunt speed progression (notes §31, §67). Three ROM mechanisms:
/// 1. Per death (R5 $3A94-3A9F): `LDA $BE5C / LDB #$E0 / MUL / CMPA $BE5D` — the
///    limit becomes the high byte of limit × 224 (limit × 7/8, TRUNCATED) ONLY
///    if that is still ≥ the floor; otherwise the limit is left ALONE. The
///    in-flight countdown is NOT re-rolled.
/// 2. Level-progress tick (R5 $2AC7-2AF1): every 225 vblanks. The first is 277 vblanks after the game goes
///    live (`CLR STATUS` at PLS2, R5 $289A): 22 to reach GEXEC (`NAP 12`, `NAP 10`), then 17 sleeps of 15, because
///    GEXEC loads 18 and counts down on its very first pass. A score made before GEXEC starts is forgotten
///    (`CLR SCRFLG`, R5 $2A8B). It runs
///    ONLY while FEWER than 30 grunts are alive (`CMPA #30 / BHS` skips it at 30 or more):
///    the floor drops by 2 and the limit by 4, or by 1 and 2 when the player has
///    scored since the last pass (SCRFLG, $F0), the limit clamped to the floor.
/// 3. The floor reaches 1 = the arcade player's speed (player deltas ±1
///    arcade px/frame at $3031; a grunt at limit 1 steps 4 arcade px every
///    4-vblank beat = 1 arcade px/frame) — grunts end "at least as fast as
///    the player" (author-verified against source + disasm).
/// </summary>
public sealed class GruntSpeedProgressTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    /// <summary>ROM frames from the game going live to GEXEC starting: `NAP 12,PLS3` and `NAP 10,PLS4`.</summary>
    private const int ExecutiveStartRomFrames = 12 + 10;

    /// <summary>ROM frames from the game going live to the first check: GEXEC's 18th pass is 17 sleeps of 15 after it starts.</summary>
    private const int FirstCheckRomFrames = ExecutiveStartRomFrames + (17 * 15);

    /// <summary>ROM frames between one check and the next: 15 passes of 15.</summary>
    private const int CheckIntervalRomFrames = 15 * 15;

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    /// <summary>The port tick, counted from the field being made, on which a ROM frame that many after the game goes live falls.</summary>
    private static int TickOfRomFrameAfterLive(PlayField field, int romFrames) => ArcadeClock.ToPortTicksRoundedUp(field.GetLiveRomFrames() + romFrames);

    private static void Tick(PlayField field, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            field.Update(Frame());
        }
    }

    private static PlayField CreateField(int wave = 1) => new PlayFieldBuilder().WithParameters(LevelParameters.CreateFromWave(wave, WaveTable.GetParameters(wave))).WithSeed(1).Build();

    private static Grunt CreateGruntAt(PlayField field, int x, int y, int seed) =>
        new(TestSprites.Shared, new IntVector2(x, y), moveLimitBeats: 20, random: new Random(seed));

    [Fact]
    public void SpeedUp_TruncatesTo7Eighths_AndRespectsTheCurrentFloor()
    {
        Grunt grunt = CreateGruntAt(CreateField(), 100, 100, 1);
        Assert.Equal(20, grunt.MoveDelayBeats);

        // 20 × 224/256 = 17.5 → the ROM's MUL high byte truncates to 17.
        grunt.SpeedUp(floorBeats: 1);
        Assert.Equal(17, grunt.MoveDelayBeats);

        // 17 × 7/8 = 14.875 → 14, which is BELOW the floor 19, so the ROM's
        // `BCS` branch leaves the limit UNCHANGED at 17 (it does not clamp UP to
        // the floor — that was the port's bug, and it made the grunts faster than
        // the arcade's).
        grunt.SpeedUp(floorBeats: 19);
        Assert.Equal(17, grunt.MoveDelayBeats);

        // With a floor the next step can respect, the limit does move.
        grunt.SpeedUp(floorBeats: 14);
        Assert.Equal(14, grunt.MoveDelayBeats);

        // And it stops there: 14 × 7/8 = 12 < 14. The in-flight countdown is
        // deliberately untouched by a kill (the ROM only changes the shared limit),
        // so the survivors accelerate on their own next re-roll.
    }

    [Fact]
    public void WaveSpeedTick_LowersLimitBy4_ClampedAtTheFloor()
    {
        Grunt grunt = CreateGruntAt(CreateField(), 100, 100, 2);

        grunt.WaveSpeedTick(floorBeats: 9);
        Assert.Equal(16, grunt.MoveDelayBeats);

        grunt.WaveSpeedTick(floorBeats: 9);
        Assert.Equal(12, grunt.MoveDelayBeats);

        grunt.WaveSpeedTick(floorBeats: 9);
        Assert.Equal(9, grunt.MoveDelayBeats); // clamped, does not go below the floor

        grunt.WaveSpeedTick(floorBeats: 1);
        Assert.Equal(5, grunt.MoveDelayBeats); // 9 − 4, floor 1
    }

    [Fact]
    public void Floor_DescendsFirst277VblanksAfterTheGameGoesLive_ThenEvery225_OnlyWithFewerThan30Grunts()
    {
        // Wave 7 (ROM $2E24): 0 grunts, 0 ELECTRODES — with the stationary
        // player nothing can kill the 29 grunts we add, so the exact cadence
        // stays deterministic. Wave 7 floor = 5, ROBSPD = 15.
        PlayField field = CreateField(wave: 7);
        Assert.Equal(5, field.GruntSpeedFloor); // wave 7: RMXSPD = 5, 0 electrodes

        Grunt? tracked = null;
        for (int i = 0; i < 29; i++)
        {
            Grunt grunt = new(TestSprites.Shared, new IntVector2(100 + (i % 5) * 24, 100 + (i / 5) * 24), moveLimitBeats: 15, random: new Random(i));
            if (i == 0)
            {
                tracked = grunt;
            }

            field.Entities.Grunts.Add(grunt);
        }

        Assert.Equal(15, tracked!.MoveDelayBeats);

        // The start of the wave, then one tick short of the first check: the floor must be untouched.
        int firstCheckTick = TickOfRomFrameAfterLive(field, FirstCheckRomFrames);
        Tick(field, firstCheckTick - 1);

        Assert.Equal(5, field.GruntSpeedFloor);
        Assert.Equal(15, tracked.MoveDelayBeats);

        field.Update(Frame()); // first level-progress tick (277 vblanks after the game went live)

        Assert.Equal(3, field.GruntSpeedFloor); // 5 − 2
        Assert.Equal(11, tracked.MoveDelayBeats); // 15 − 4

        // One tick short of the next check: unchanged.
        int secondCheckTick = TickOfRomFrameAfterLive(field, FirstCheckRomFrames + CheckIntervalRomFrames);
        Tick(field, secondCheckTick - firstCheckTick - 1);

        Assert.Equal(3, field.GruntSpeedFloor);

        field.Update(Frame()); // second tick (225 vblanks after the first)

        // Nothing was scored since the first pass, so this one is the harsh one again: the
        // floor drops by 2 (3 → 1) and the limit by 4 (11 → 7).
        Assert.Equal(1, field.GruntSpeedFloor);
        Assert.Equal(7, tracked.MoveDelayBeats);
    }

    [Fact]
    public void APassThatFollowsScoring_IsGentler_AndTheFlagIsSpentByIt()
    {
        PlayField field = CreateField(wave: 7);
        Grunt tracked = new(TestSprites.Shared, new IntVector2(100, 100), moveLimitBeats: 15, random: new Random(1));
        field.Entities.Grunts.Add(tracked);

        // The score must come after GEXEC has started, because GEXEC opens by clearing the flag.
        int executiveStartTick = TickOfRomFrameAfterLive(field, ExecutiveStartRomFrames);
        int firstCheckTick = TickOfRomFrameAfterLive(field, FirstCheckRomFrames);
        Tick(field, executiveStartTick);
        field.AwardScore(100); // SCRFLG is set by any score (ROM UPDATE_PLAYER_SCORE, $DB9C)
        Tick(field, firstCheckTick - executiveStartTick);

        Assert.Equal(4, field.GruntSpeedFloor); // 5 − 1, not 5 − 2
        Assert.Equal(13, tracked.MoveDelayBeats); // 15 − 2, not 15 − 4

        // The pass cleared the flag, so with no further score the next one is the harsh one.
        Tick(field, TickOfRomFrameAfterLive(field, FirstCheckRomFrames + CheckIntervalRomFrames) - firstCheckTick);

        Assert.Equal(2, field.GruntSpeedFloor); // 4 − 2
        Assert.Equal(9, tracked.MoveDelayBeats); // 13 − 4
    }

    [Fact]
    public void AScoreMadeBeforeTheExecutiveStarts_IsForgotten_SoTheFirstPassIsTheHarshOne()
    {
        PlayField field = CreateField(wave: 7);
        Grunt tracked = new(TestSprites.Shared, new IntVector2(100, 100), moveLimitBeats: 15, random: new Random(1));
        field.Entities.Grunts.Add(tracked);

        // GEXEC opens with CLR SCRFLG (R5 $2A8B), 22 vblanks after the game goes live, so this score does not count.
        int scoreTick = WaveStartTicks.UntilLive(field) + 1;
        Tick(field, scoreTick);
        field.AwardScore(100);
        Tick(field, TickOfRomFrameAfterLive(field, FirstCheckRomFrames) - scoreTick);

        Assert.Equal(3, field.GruntSpeedFloor); // 5 − 2, not 5 − 1
        Assert.Equal(11, tracked.MoveDelayBeats); // 15 − 4, not 15 − 2
    }

    [Fact]
    public void Floor_Holds_WhileThirtyOrMoreGruntsAreAlive()
    {
        PlayField field = CreateField();
        for (int i = 0; i < 30; i++)
        {
            field.Entities.Grunts.Add(CreateGruntAt(field, 20 + (i % 10) * 24, 20 + (i / 10) * 24, i));
        }

        // The start of the wave and two full checks with 30 grunts on screen: the $2ACA gate (cur_grunts >= 30, BCC) skips the update.
        Tick(field, TickOfRomFrameAfterLive(field, FirstCheckRomFrames + CheckIntervalRomFrames));

        Assert.Equal(9, field.GruntSpeedFloor);
    }
}