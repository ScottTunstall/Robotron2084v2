using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
/// Notes 27 (playtest round 9: "when the sparks hit the wall, they don't
/// immediately fizzle out, sometimes they linger for a while (and keep
/// moving)") — the port re-checked against the R5 disasm:
/// - life = RND(0..15)+20 cycles x 4 vblanks = 80..140 ROM ticks
///   (1.6-2.8 s), NOT the spec's 10-15 s;
/// - the mover ($DCFF) REJECTS an axis update that would push the picture
///   out of the playfield: the spark clamps/slides at the wall and dies on
///   its life — it never leaves the playfield and never dies at the wall;
/// - aim = per-axis velocity proportional to player distance (ROM delta =
///   4 x (distance + RND(0..31)-16) subpixels/frame), with a random
///   curvature integrated into the velocity.
/// </summary>
public sealed class SparkTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int seed) =>
        new(TestSprites.Shared, 
            new LevelParameters(
                LevelNumber: 1,
                SpheroidCount: 0,
                MaxDropsX2: 10,
                SpheroidDropDelay: 30),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(seed),
            startingLives: 3);

    [Fact]
    public void Spark_ClampsAtTheWall_AndDiesOnItsLife_NotAtTheWall()
    {
        PlayField field = CreateField(1);
        Rectangle inner = field.Wall.PlayfieldBounds;

        // ~265 screen px right of the left wall, aimed hard at the wall
        // (vx ≈ -265/53 + jitter = -4..-6 px/tick).
        IntVector2 origin = new(inner.X + 280, inner.Y + 50);
        field.SpawnSpark(origin, new IntVector2(inner.X + 15, inner.Y + 50));
        Spark spark = Assert.Single(field.Sparks);

        for (int tick = 1; tick <= 250; tick++)
        {
            field.Update(Frame());
            if (spark.LifeState == EntityLifeState.Dead)
            {
                break;
            }

            // Never leaves the playfield, even pinned against the wall.
            Assert.True(spark.Bounds.X >= inner.X, $"tick {tick}: x {spark.Bounds.X} < {inner.X}");
            Assert.True(spark.Bounds.Right <= inner.Right, $"tick {tick}: right {spark.Bounds.Right} > {inner.Right}");
        }

        // It died of its ROM life (<= 168 ticks), not of the wall.
        Assert.Equal(EntityLifeState.Dead, spark.LifeState);
    }

    [Fact]
    public void Spark_Life_Is80To140RomTicks()
    {
        PlayField field = CreateField(2);
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 centre = new(inner.X + inner.Width / 2, inner.Y + inner.Height / 2);

        for (int seed = 1; seed <= 16; seed++)
        {
            field.SpawnSpark(centre, new IntVector2(centre.X + 1, centre.Y)); // +1 keeps vx non-zero
            Spark spark = field.Sparks[^1];

            int diedAt = -1;
            for (int tick = 1; tick <= 400; tick++)
            {
                field.Update(Frame());
                if (spark.LifeState == EntityLifeState.Dead)
                {
                    diedAt = tick;
                    break;
                }
            }

            Assert.True(diedAt > 0, $"seed {seed}: spark never died");
            int minTicks = GameplayConstants.PortTicks(GameplayConstants.SparkLifeMinRomTicks);
            int maxTicks = GameplayConstants.PortTicks(GameplayConstants.SparkLifeMaxRomTicks);
            Assert.InRange(diedAt, minTicks, maxTicks);
        }
    }

    [Fact]
    public void Spark_Flicker_CyclesAllFourFrames_AtFourVblanksPerFrame()
    {
        // Notes 32: the ROM SPARK process advances OPICT one SPKP entry per
        // beat pass, re-running every 4 vblanks (NAP 4) → 4-frame flash.
        PlayField field = CreateField(4);
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 centre = new(inner.X + inner.Width / 2, inner.Y + inner.Height / 2);
        field.SpawnSpark(centre, new IntVector2(centre.X + 1, centre.Y));
        Spark spark = field.Sparks[^1];

        // 4 ROM frames = 4.8 ticks, so the frame boundary lands on tick 5 of each
        // period on the exact-6ths clock (notes §52, §65).
        int period = GameplayConstants.PortTicksCeil(GameplayConstants.SparkFramePeriodRomTicks);
        Assert.Equal(0, spark.FrameIndex); // born on SPKP0

        // Each full period advances exactly one frame; four periods wrap to 0.
        for (int frame = 1; frame <= 4; frame++)
        {
            for (int tick = 1; tick < period; tick++)
            {
                field.Update(Frame());
                Assert.Equal(frame - 1, spark.FrameIndex); // stable within a period
            }

            field.Update(Frame());
            int expected = frame % SpriteSet.SparkFrameCount;
            Assert.Equal(expected, spark.FrameIndex);
        }
    }

    [Fact]
    public void Spark_AimsAtThePlayer_WithDistanceProportionalSpeed()
    {
        PlayField field = CreateField(3);
        Rectangle inner = field.Wall.PlayfieldBounds;

        // GOSPEL (RRC11.ASM ENFSHT + the generic mover RRS22.ASM OPB80):
        // OXV = 4 x delta and the mover adds the FULL 16-bit velocity to the
        // position once per FRAME, so a spark covers delta/64 COLUMNS per frame
        // — here 300/64 ≈ 4.7 columns ≈ 4.7 port px... per FRAME, and with the
        // per-axis jitter of (seed & $1F) - 16 columns = ±16 columns = ±64 port
        // px folded into the delta: after 4 frames the X has moved delta/16 for
        // delta ∈ [-(300+64), -(300-60)] → about -15 to -23 px.
        IntVector2 origin = new(inner.X + 300, inner.Y + 100);
        field.SpawnSpark(origin, new IntVector2(inner.X, inner.Y + 100));
        Spark spark = field.Sparks[^1];

        for (int tick = 0; tick < 4; tick++)
        {
            field.Update(Frame());
        }

        Assert.InRange(spark.Position.X - origin.X, -24, -13);
        // Y had zero aim distance: only the jitter (±64 port px → ±1 px per
        // frame) can have shown.
        Assert.InRange(spark.Position.Y - origin.Y, -5, 5);
    }

    /// <summary>
    /// The ROM gives a spark a CONSTANT per-axis acceleration (PD2/PD4, chosen
    /// once at spawn and fixed for its life), applied once per move — so the
    /// velocity changes by the SAME amount every move and the path is a
    /// PARABOLA. The previous port re-randomised a nudge instead, which was a
    /// drunken walk. This is the behaviour the author asked to be fixed.
    /// </summary>
    [Fact]
    public void Spark_VelocityChangesByAConstantAcceleration_EveryMove()
    {
        PlayField field = CreateField(3);
        Rectangle inner = field.Wall.PlayfieldBounds;

        // Well away from the left wall so the X jitter is NOT suppressed, and
        // far enough that the aim dominates the jitter.
        IntVector2 origin = new(inner.X + 320, inner.Y + 200);
        field.SpawnSpark(origin, new IntVector2(inner.X + 40, inner.Y + 120));
        Spark spark = field.Sparks[^1];

        int moveInterval = GameplayConstants.PortTicksCeil(GameplayConstants.SparkMoveIntervalRomTicks);
        var changes = new List<IntVector2>();
        IntVector2 previous = spark.VelocitySubpixels;

        for (int move = 0; move < 6; move++)
        {
            for (int tick = 0; tick < moveInterval; tick++)
            {
                field.Update(Frame());
            }

            changes.Add(new IntVector2(spark.VelocitySubpixels.X - previous.X, spark.VelocitySubpixels.Y - previous.Y));
            previous = spark.VelocitySubpixels;
        }

        // Every move must apply the SAME delta on each axis: constant acceleration.
        IntVector2 expected = spark.AccelerationSubpixels;
        Assert.All(changes, c => Assert.Equal(expected, c));

        // ...and it must be the acceleration the spark was built with, i.e. the
        // ROM's (seed & $1F) - 16 range scaled by the fixed-point factor.
        int subpixelsPerPortPxPerMove = GameplayConstants.SparkVelocityScale / GameplayConstants.SparkAimDivisor;
        int maxAccelSubpixels = GameplayConstants.SparkAccelRomRange * subpixelsPerPortPxPerMove;
        Assert.InRange(expected.X, -maxAccelSubpixels, maxAccelSubpixels);
        Assert.InRange(expected.Y, -maxAccelSubpixels, maxAccelSubpixels);
    }
}