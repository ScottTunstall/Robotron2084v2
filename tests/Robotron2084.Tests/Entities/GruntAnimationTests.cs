using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// R5 grunt stagger (notes §29): beat every 4 vblanks; the move countdown
/// re-rolls to RND(1..ROBSPD) (never 0) on each step; a step moves 8 screen
/// px on each axis past a 4-screen-px dead zone (axes independent); the
/// RWDP frame advances ONLY on a step (DRAW_GRUNT is in the step branch —
/// a paused grunt freezes mid-pose). Frame table maps
/// RWDP1/2/3/4 → arts RWDD1/RWDD2/RWDD1/RWDD3 (3 unique, [A,B,A,C]).
/// </summary>
public sealed class GruntAnimationTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField() => new PlayFieldBuilder().WithParameters(new LevelParameters(LevelNumber: 1)).WithSeed(1).Build();

    /// <summary>Drives the field through the start of the wave, until the game is live.</summary>
    private static void RunUntilLive(PlayField field)
    {
        for (int tick = 0; tick < WaveStartTicks.UntilLive(field); tick++)
        {
            field.Update(Frame());
        }
    }

    [Theory]
    [InlineData(1, 0)] // RWDP1 → RWDD1
    [InlineData(2, 1)] // RWDP2 → RWDD2
    [InlineData(3, 0)] // RWDP3 → RWDD1 (the ROM reuses the first animation frame)
    [InlineData(4, 2)] // RWDP4 → RWDD3
    public void RomAnimationFrameTable_MapsTheFourRWDPFramesOntoThreeRepoFrames(int romFrame, int repoIndex)
    {
        Assert.Equal(repoIndex, Grunt.GetAnimationFrameIndex(romFrame));
    }

    [Fact]
    public void Grunt_StepsTwoColumnsSidewaysAndFourRowsUpAndDown_AndBobsWhenLevelWithThePlayer()
    {
        PlayField field = CreateField();
        RunUntilLive(field);

        // Lone grunt 200px left of the player on the same row. ROM MOVE_GRUNT: sideways it
        // steps 2 columns (8 port px) towards the player every time; level with the player
        // it steps DOWN 4 rows (8 port px), then back up, so it bobs rather than staying put.
        IntVector2 player = field.Player.Position;
        Grunt grunt = new(TestSprites.Shared, new IntVector2(player.X - 200, player.Y), moveLimitBeats: 15, random: new Random(3));
        IntVector2 last = grunt.Position;

        int steps = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            grunt.Update(Frame(), field);
            if (grunt.Position == last)
            {
                continue;
            }

            int dx = grunt.Position.X - last.X;
            int dy = grunt.Position.Y - last.Y;
            Assert.Equal(8, dx);
            Assert.Equal(8, Math.Abs(dy));
            last = grunt.Position;
            steps++;
        }

        Assert.True(steps >= 8, $"expected a steady stream of 8px steps, saw {steps}");
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(2, -8)]
    [InlineData(-2, 8)]
    public void Grunt_IgnoresAnUpAndDownGapOfExactlyOneRow_ButStepsForAnyOther(int rowsBelowPlayer, int expectedDy)
    {
        // ROM MOVE_GRUNT: `CMPB #$FE` and `CMPB #$02` skip the up-and-down step only when the
        // grunt is exactly one row above or below the player.
        PlayField field = CreateField();
        RunUntilLive(field);

        IntVector2 player = field.Player.Position;
        Grunt grunt = new(TestSprites.Shared, new IntVector2(player.X - 200, player.Y + (rowsBelowPlayer * ScreenSize.ToPortPixelsFromArcadePixels(1))), moveLimitBeats: 15, random: new Random(3));
        int startY = grunt.Position.Y;

        for (int tick = 0; tick < 600 && !grunt.SteppedThisUpdate; tick++)
        {
            grunt.Update(Frame(), field);
        }

        Assert.True(grunt.SteppedThisUpdate);
        Assert.Equal(startY + expectedDy, grunt.Position.Y);
    }

    [Fact]
    public void Grunt_StepGaps_AreIrregular_AndBoundedByTheRerollLimit()
    {
        PlayField field = CreateField();
        RunUntilLive(field);

        IntVector2 player = field.Player.Position;
        Grunt grunt = new(TestSprites.Shared, new IntVector2(player.X - 300, player.Y - 120), moveLimitBeats: 15, random: new Random(7));

        IntVector2 last = grunt.Position;
        var stepTicks = new List<int>();
        for (int tick = 1; tick <= 1200; tick++)
        {
            grunt.Update(Frame(), field);
            if (grunt.Position != last)
            {
                stepTicks.Add(tick);
                last = grunt.Position;
            }
        }

        Assert.True(stepTicks.Count >= 10, $"expected many steps in 1200 ticks, saw {stepTicks.Count}");

        int minGap = int.MaxValue;
        int maxGap = 0;
        var gaps = new HashSet<int>();
        for (int i = 1; i < stepTicks.Count; i++)
        {
            int gap = stepTicks[i] - stepTicks[i - 1];
            minGap = Math.Min(minGap, gap);
            maxGap = Math.Max(maxGap, gap);
            gaps.Add(gap);
        }

        // Each gap = countdown re-roll (1..15 beats) x 4-vblank beat.
        Assert.InRange(minGap, ArcadeClock.ToPortTicks(4), ArcadeClock.ToPortTicksRoundedUp(4) * 15);
        Assert.InRange(maxGap, ArcadeClock.ToPortTicks(4), ArcadeClock.ToPortTicksRoundedUp(4) * 15);
        // The stagger: NOT a fixed period — at least two distinct gaps.
        Assert.True(gaps.Count >= 2, $"step gaps were constant {gaps.First()}");
    }

    [Fact]
    public void Grunt_WalkFrameFreezesWhilePaused_AndAdvancesOncePerStep()
    {
        PlayField field = CreateField();
        RunUntilLive(field);

        IntVector2 player = field.Player.Position;
        // Huge re-roll limit: no steps for a long time — beats keep firing.
        // DRAW_GRUNT ($3A2B) is only reached from the step branch, so the
        // legs must FREEZE while the grunt is paused (playtest round 12:
        // "even when they are standing still, their legs are moving").
        Grunt grunt = new(TestSprites.Shared, new IntVector2(player.X - 300, player.Y - 300), moveLimitBeats: 300, random: new Random(11));

        Assert.Equal(1, grunt.WalkAnimationFrameNumber);
        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(4) * 10; tick++)
        {
            grunt.Update(Frame(), field);
        }

        Assert.Equal(1, grunt.WalkAnimationFrameNumber); // 10 beat passes, no step, no frame change

        // Now let it walk: exactly one frame advance per completed step. (Check
        // that the frame CHANGED at least once rather than what it ended on: the
        // 4-frame cycle wraps, so an exact multiple of 4 steps lands back on 1.)
        IntVector2 last = grunt.Position;
        bool advanced = false;
        for (int tick = 0; tick < 6000; tick++)
        {
            grunt.Update(Frame(), field);
            if (grunt.Position != last)
            {
                last = grunt.Position;
            }

            if (grunt.WalkAnimationFrameNumber != 1)
            {
                advanced = true;
            }
        }

        Assert.True(advanced, "walk frame never advanced while stepping");
    }
}