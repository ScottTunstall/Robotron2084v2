using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     Grunt speed progression (notes §31, §67). Three ROM mechanisms:
///     1. Per death (R5 $3A94-3A9F): `LDA $BE5C / LDB #$E0 / MUL / CMPA $BE5D` — the
///     limit becomes the high byte of limit × 224 (limit × 7/8, TRUNCATED) ONLY
///     if that is still ≥ the floor; otherwise the limit is left ALONE. The
///     in-flight countdown is NOT picked again.
///     2. Wave-progress tick (R5 $2AC7-2AF1): every 225 arcade frames. The first is 277 arcade frames after the game goes
///     live (`CLR STATUS` at PLS2, R5 $289A): 22 to reach GEXEC (`NAP 12`, `NAP 10`), then 17 sleeps of 15, because
///     GEXEC loads 18 and counts down on its very first pass. A score made before GEXEC starts is forgotten
///     (`CLR SCRFLG`, R5 $2A8B). It runs
///     ONLY while FEWER than 30 grunts are alive (`CMPA #30 / BHS` skips it at 30 or more):
///     the floor drops by 2 and the limit by 4, or by 1 and 2 when the player has
///     scored since the last pass (SCRFLG, $F0), the limit clamped to the floor.
///     3. The floor reaches 1 = the arcade player's speed (player deltas ±1
///     arcade px/frame at $3031; a grunt at limit 1 steps 4 arcade px every
///     4-arcade frame beat = 1 arcade px/frame) — grunts end "at least as fast as
///     the player" (author-verified against source + disasm).
/// </summary>
public sealed class GruntSpeedProgressTests
{
    /// <summary>fiftieths of a second from the game going live to GEXEC starting: `NAP 12,PLS3` and `NAP 10,PLS4`.</summary>
    private const int ExecutiveStartArcadeFrames = 12 + 10;

    /// <summary>
    ///     fiftieths of a second from the game going live to the first check: GEXEC's 18th pass is 17 sleeps of 15 after
    ///     it starts.
    /// </summary>
    private const int FirstCheckArcadeFrames = ExecutiveStartArcadeFrames + 17 * 15;

    /// <summary>fiftieths of a second between one check and the next: 15 passes of 15.</summary>
    private const int CheckIntervalArcadeFrames = 15 * 15;

    private static readonly TimeSpan OneTickSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, OneTickSpan);
    }

    /// <summary>
    ///     The port tick, counted from the field being made, on which a fiftieth of a second that many after the game
    ///     goes live falls.
    /// </summary>
    private static int TickOfRomFrameAfterLive(PlayField playField, int arcadeFrames)
    {
        return ArcadeClock.ToPortTicksRoundedUp(playField.GetLiveArcadeFrames() + arcadeFrames);
    }

    private static void Tick(PlayField playField, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) playField.Update(OneTick());
    }

    private static PlayField CreateField(int wave = 1)
    {
        return new PlayFieldBuilder()
            .WithParameters(WaveParameters.CreateFromWave(wave, WaveTable.GetParameters(wave))).WithSeed(1).Build();
    }

    private static Grunt CreateGruntAt(PlayField playField, int x, int y, int seed)
    {
        return new Grunt(TestSprites.SharedSpriteSet, new IntVector2(x, y), 20, new Random(seed));
    }

    [Fact]
    public void SpeedUp_TruncatesTo7Eighths_AndRespectsTheCurrentFloor()
    {
        var grunt = CreateGruntAt(CreateField(), 100, 100, 1);
        Assert.Equal(20, grunt.MoveDelayBeats);

        // 20 × 224/256 = 17.5 → the ROM's MUL high byte truncates to 17.
        grunt.SpeedUp(1);
        Assert.Equal(17, grunt.MoveDelayBeats);

        // 17 × 7/8 = 14.875 → 14, which is BELOW the floor 19, so the ROM's
        // `BCS` branch leaves the limit UNCHANGED at 17 (it does not clamp UP to
        // the floor — that was the port's bug, and it made the grunts faster than
        // the arcade's).
        grunt.SpeedUp(19);
        Assert.Equal(17, grunt.MoveDelayBeats);

        // With a floor the next step can respect, the limit does move.
        grunt.SpeedUp(14);
        Assert.Equal(14, grunt.MoveDelayBeats);

        // And it stops there: 14 × 7/8 = 12 < 14. The in-flight countdown is
        // deliberately untouched by a kill (the ROM only changes the shared limit),
        // so the survivors accelerate on their own next pick.
    }

    [Fact]
    public void SpeedUpAsWaveGoesOn_LowersLimitBy4_ClampedAtTheFloor()
    {
        var grunt = CreateGruntAt(CreateField(), 100, 100, 2);

        grunt.SpeedUpAsWaveGoesOn(9);
        Assert.Equal(16, grunt.MoveDelayBeats);

        grunt.SpeedUpAsWaveGoesOn(9);
        Assert.Equal(12, grunt.MoveDelayBeats);

        grunt.SpeedUpAsWaveGoesOn(9);
        Assert.Equal(9, grunt.MoveDelayBeats); // clamped, does not go below the floor

        grunt.SpeedUpAsWaveGoesOn(1);
        Assert.Equal(5, grunt.MoveDelayBeats); // 9 − 4, floor 1
    }

    [Fact]
    public void Floor_DescendsFirst277ArcadeFramesAfterTheGameGoesLive_ThenEvery225_OnlyWithFewerThan30Grunts()
    {
        // Wave 7 (ROM $2E24): 0 grunts, 0 ELECTRODES — with the stationary
        // player nothing can kill the 29 grunts we add, so the exact cadence
        // stays deterministic. Wave 7 floor = 5, ROBSPD = 15.
        var playField = CreateField(7);
        Assert.Equal(5, playField.GetGruntSpeedFloor()); // wave 7: RMXSPD = 5, 0 electrodes

        Grunt? trackedGrunt = null;
        for (var i = 0; i < 29; i++)
        {
            Grunt grunt = new(TestSprites.SharedSpriteSet, new IntVector2(100 + i % 5 * 24, 100 + i / 5 * 24), 15,
                new Random(i));
            if (i == 0) trackedGrunt = grunt;

            playField.FieldEntities.Grunts.Add(grunt);
        }

        Assert.Equal(15, trackedGrunt!.MoveDelayBeats);

        // The start of the wave, then one tick short of the first check: the floor must be untouched.
        var firstCheckTick = TickOfRomFrameAfterLive(playField, FirstCheckArcadeFrames);
        Tick(playField, firstCheckTick - 1);

        Assert.Equal(5, playField.GetGruntSpeedFloor());
        Assert.Equal(15, trackedGrunt.MoveDelayBeats);

        playField.Update(OneTick()); // first wave-progress tick (277 arcade frames after the game went live)

        Assert.Equal(3, playField.GetGruntSpeedFloor()); // 5 − 2
        Assert.Equal(11, trackedGrunt.MoveDelayBeats); // 15 − 4

        // One tick short of the next check: unchanged.
        var secondCheckTick = TickOfRomFrameAfterLive(playField, FirstCheckArcadeFrames + CheckIntervalArcadeFrames);
        Tick(playField, secondCheckTick - firstCheckTick - 1);

        Assert.Equal(3, playField.GetGruntSpeedFloor());

        playField.Update(OneTick()); // second tick (225 arcade frames after the first)

        // Nothing was scored since the first pass, so this one is the harsh one again: the
        // floor drops by 2 (3 → 1) and the limit by 4 (11 → 7).
        Assert.Equal(1, playField.GetGruntSpeedFloor());
        Assert.Equal(7, trackedGrunt.MoveDelayBeats);
    }

    [Fact]
    public void APassThatFollowsScoring_IsGentler_AndTheFlagIsSpentByIt()
    {
        var playField = CreateField(7);
        Grunt trackedGrunt = new(TestSprites.SharedSpriteSet, new IntVector2(100, 100), 15, new Random(1));
        playField.FieldEntities.Grunts.Add(trackedGrunt);

        // The score must come after GEXEC has started, because GEXEC opens by clearing the flag.
        var executiveStartTick = TickOfRomFrameAfterLive(playField, ExecutiveStartArcadeFrames);
        var firstCheckTick = TickOfRomFrameAfterLive(playField, FirstCheckArcadeFrames);
        Tick(playField, executiveStartTick);
        playField.AwardScore(100); // SCRFLG is set by any score (ROM UPDATE_PLAYER_SCORE, $DB9C)
        Tick(playField, firstCheckTick - executiveStartTick);

        Assert.Equal(4, playField.GetGruntSpeedFloor()); // 5 − 1, not 5 − 2
        Assert.Equal(13, trackedGrunt.MoveDelayBeats); // 15 − 2, not 15 − 4

        // The pass cleared the flag, so with no further score the next one is the harsh one.
        Tick(playField, TickOfRomFrameAfterLive(playField, FirstCheckArcadeFrames + CheckIntervalArcadeFrames) - firstCheckTick);

        Assert.Equal(2, playField.GetGruntSpeedFloor()); // 4 − 2
        Assert.Equal(9, trackedGrunt.MoveDelayBeats); // 13 − 4
    }

    [Fact]
    public void AScoreMadeBeforeTheExecutiveStarts_IsForgotten_SoTheFirstPassIsTheHarshOne()
    {
        var playField = CreateField(7);
        Grunt trackedGrunt = new(TestSprites.SharedSpriteSet, new IntVector2(100, 100), 15, new Random(1));
        playField.FieldEntities.Grunts.Add(trackedGrunt);

        // GEXEC opens with CLR SCRFLG (R5 $2A8B), 22 arcade frames after the game goes live, so this score does not count.
        var scoreTick = WaveStartTicks.UntilLive(playField) + 1;
        Tick(playField, scoreTick);
        playField.AwardScore(100);
        Tick(playField, TickOfRomFrameAfterLive(playField, FirstCheckArcadeFrames) - scoreTick);

        Assert.Equal(3, playField.GetGruntSpeedFloor()); // 5 − 2, not 5 − 1
        Assert.Equal(11, trackedGrunt.MoveDelayBeats); // 15 − 4, not 15 − 2
    }

    [Fact]
    public void Floor_Holds_WhileThirtyOrMoreGruntsAreAlive()
    {
        var playField = CreateField();
        for (var i = 0; i < 30; i++)
            playField.FieldEntities.Grunts.Add(CreateGruntAt(playField, 20 + i % 10 * 24, 20 + i / 10 * 24, i));

        // The start of the wave and two full checks with 30 grunts on screen: the $2ACA gate (cur_grunts >= 30, BCC) skips the update.
        Tick(playField, TickOfRomFrameAfterLive(playField, FirstCheckArcadeFrames + CheckIntervalArcadeFrames));

        Assert.Equal(9, playField.GetGruntSpeedFloor());
    }
}
