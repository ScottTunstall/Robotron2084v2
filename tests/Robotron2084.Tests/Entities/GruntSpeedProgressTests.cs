using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Grunt speed progression (notes §31, §67). Three ROM mechanisms:
/// 1. Per death (R5 $3A94-3A9F): `LDA $BE5C / LDB #$E0 / MUL / CMPA $BE5D` — the
///    limit becomes the high byte of limit × 224 (limit × 7/8, TRUNCATED) ONLY
///    if that is still ≥ the floor; otherwise the limit is left ALONE. The
///    in-flight countdown is NOT re-rolled.
/// 2. Level-progress tick (R5 $2AC7-2AF1): every 225 vblanks (first at 270),
///    ONLY while 30+ grunts are alive: the floor drops by 2 and the limit by 4 on
///    one pass, then by 1 and 2 on the next ($F0 toggles), the limit clamped to
///    the floor.
/// 3. The floor reaches 1 = the arcade player's speed (player deltas ±1
///    arcade px/frame at $3031; a grunt at limit 1 steps 4 arcade px every
///    4-vblank beat = 1 arcade px/frame) — grunts end "at least as fast as
///    the player" (author-verified against source + disasm).
/// </summary>
public sealed class GruntSpeedProgressTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int wave = 1) => new(TestSprites.Shared, 
        LevelParameters.FromWave(wave, WaveTable.ForWave(wave)),
        new FakeInputSource(),
        PlayFieldSpawnTests.InnerBounds,
        new WallColorCycle(),
        new Random(1),
        startingLives: 3);

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
    public void Floor_DescendsOnThe270Then225VblankCadence_OnlyWith30PlusGrunts()
    {
        // Wave 7 (ROM $2E24): 0 grunts, 0 ELECTRODES — with the stationary
        // player nothing can kill the 30 grunts we add, so the exact cadence
        // stays deterministic. Wave 7 floor = 5, ROBSPD = 15.
        PlayField field = CreateField(wave: 7);
        Assert.Equal(5, field.GruntSpeedFloor); // wave 7: RMXSPD = 5, 0 electrodes

        Grunt? tracked = null;
        for (int i = 0; i < 30; i++)
        {
            Grunt grunt = new(TestSprites.Shared, new IntVector2(100 + (i % 5) * 24, 100 + (i / 5) * 24), moveLimitBeats: 15, random: new Random(i));
            if (i == 0)
            {
                tracked = grunt;
            }

            field.AddGrunt(grunt);
        }

        Assert.Equal(15, tracked!.MoveDelayBeats);

        // 121 grace frames + 203 = 324 = PortTicks(270): one frame before the
        // first tick the floor must be untouched.
        for (int tick = 0; tick < 121 + (GameplayConstants.PortTicks(270) - 121) - 1; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(5, field.GruntSpeedFloor);
        Assert.Equal(15, tracked.MoveDelayBeats);

        field.Update(Frame()); // first level-progress tick (270 vblanks in)

        Assert.Equal(3, field.GruntSpeedFloor); // 5 − 2
        Assert.Equal(11, tracked.MoveDelayBeats); // 15 − 4

        // 270 = PortTicks(225): one frame before the next tick, unchanged.
        for (int tick = 0; tick < GameplayConstants.PortTicks(225) - 1; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(3, field.GruntSpeedFloor);

        field.Update(Frame()); // second tick (270 + 225 vblanks)

        // The $F0 toggle: this pass drops the floor by 1 and the limit by 2.
        Assert.Equal(2, field.GruntSpeedFloor); // 3 − 1
        Assert.Equal(9, tracked.MoveDelayBeats); // 11 − 2

        // Third pass: back to −2 / −4.
        for (int tick = 0; tick < GameplayConstants.PortTicks(225); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(1, field.GruntSpeedFloor); // 2 − 2 → 1 = the player's speed
        Assert.Equal(5, tracked.MoveDelayBeats); // 9 − 4
    }

    [Fact]
    public void Floor_Holds_WhileFewerThan30GruntsAreAlive()
    {
        PlayField field = CreateField();
        for (int i = 0; i < 5; i++)
        {
            field.AddGrunt(CreateGruntAt(field, 20 + i * 24, 20, i));
        }

        // Two full cadence periods (270 + 225 vblanks + grace) with only 5
        // grunts on screen: the $2ACA gate (cur_grunts < 30) skips the update.
        for (int tick = 0; tick < 121 + GameplayConstants.PortTicks(270) + GameplayConstants.PortTicks(225); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(9, field.GruntSpeedFloor);
    }
}