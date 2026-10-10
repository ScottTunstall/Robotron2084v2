using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>Gorf: the author's own robot, which crosses the screen in a wave dropping grunts (notes §138.2).</summary>
public sealed class GorfTests
{
    private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));

    private static PlayField CreateFieldInPlay(LevelParameters? parameters = null)
    {
        var field = new PlayFieldBuilder().WithParameters(parameters ?? new LevelParameters(1)).WithSeed(5).Build();
        for (var tick = 0; tick < 400 && field.RobotsFrozen(); tick++) field.Update(Frame);

        return field;
    }

    /// <summary>A Gorf whose rolls are chosen: the side, the ground's height and the drops.</summary>
    private static Gorf CreateGorf(PlayField field, params int[] rolls)
    {
        return new Gorf(TestSprites.Shared, new ScriptedRandom(rolls), field.GetPlayfieldBounds(), 10);
    }

    /// <summary>A Gorf that rolls as a real game would, for the tests that run it right across.</summary>
    private static Gorf CreateRollingGorf(PlayField field, int seed)
    {
        return new Gorf(TestSprites.Shared, new Random(seed), field.GetPlayfieldBounds(), 10);
    }

    /// <summary>Runs the field until the Gorf is gone, and counts the ticks it took (or gives up).</summary>
    private static int RunUntilGone(PlayField field, Gorf gorf)
    {
        var ticks = 0;
        while (gorf.IsAlive() && ticks < 3000)
        {
            field.Update(Frame);
            ticks++;
        }

        return ticks;
    }

    [Fact]
    public void AHopIsAnArchThatLeavesTheGroundPeaksHalfWayAndLandsWhereItLeft_WithoutTrigonometry()
    {
        const int hopSteps = 16;
        const int height = 16;

        Assert.Equal(0, GorfPath.GetHopHeight(0, hopSteps, height));
        Assert.Equal(height, GorfPath.GetHopHeight(hopSteps / 2, hopSteps, height));
        Assert.Equal(0, GorfPath.GetHopHeight(hopSteps, hopSteps, height));

        for (var step = 0; step <= hopSteps; step++)
        {
            var up = GorfPath.GetHopHeight(step, hopSteps, height);
            Assert.InRange(up, 0, height);
            Assert.Equal(up, GorfPath.GetHopHeight(hopSteps - step, hopSteps, height));
            if (step > 0 && step <= hopSteps / 2) Assert.True(up >= GorfPath.GetHopHeight(step - 1, hopSteps, height));
        }
    }

    [Fact]
    public void EveryHopIsSixteenPixelsHigh_ForNow()
    {
        Assert.Equal(16, GorfTuning.HopRows);
        Assert.Equal(16, GorfPath.GetHopHeight(GorfTuning.HopSteps / 2, GorfTuning.HopSteps, GorfTuning.HopRows));
    }

    [Fact]
    public void ItHopsAcrossInJumpsOfTheSameHeight_AndLandsOnTheGroundBetweenThem()
    {
        var field = CreateFieldInPlay();
        var gorf = CreateRollingGorf(field, 21);
        var groundY = gorf.Position.Y;
        var startX = gorf.Position.X;
        var stepPixels = ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns);
        var highestSeen = 0;
        var peaks = new List<int>();
        var peakOfThisHop = 0;
        var lastLanding = 0;

        for (var tick = 0; tick < 3000 && gorf.IsAlive(); tick++)
        {
            gorf.Update(Frame, field);
            var risen = groundY - gorf.Position.Y;
            highestSeen = Math.Max(highestSeen, risen);
            peakOfThisHop = Math.Max(peakOfThisHop, risen);
            Assert.InRange(risen, 0, ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows));

            var stepsTaken = Math.Abs(gorf.Position.X - startX) / stepPixels;
            if (stepsTaken > 0 && stepsTaken % GorfTuning.HopSteps == 0 && stepsTaken != lastLanding)
            {
                lastLanding = stepsTaken;
                Assert.Equal(0, risen);
                peaks.Add(peakOfThisHop);
                peakOfThisHop = 0;
            }
        }

        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows), highestSeen);
        Assert.True(peaks.Count > 3);
        Assert.All(peaks.Distinct(),
            peak => Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows), peak));
    }

    [Fact]
    public void ItStartsJustOffTheScreenOnARandomSide_AtARandomHeight()
    {
        var field = CreateFieldInPlay();
        var playfield = field.GetPlayfieldBounds();

        var fromTheLeft = CreateGorf(field, 0, 0, 0);
        var fromTheRight = CreateGorf(field, 1, 0, 0);

        Assert.True(fromTheLeft.GetBounds().Right <= playfield.X);
        Assert.True(fromTheRight.GetBounds().X >= playfield.Right);

        var high = CreateGorf(field, 0, 0, 0);
        var low = CreateGorf(field, 0, 50, 0);
        Assert.True(high.Position.Y < low.Position.Y);
        Assert.True(low.Position.Y + low.GetBounds().Height <= playfield.Bottom);
    }

    [Fact]
    public void ItCrossesTheWholeScreenAndThenIsGone_WithNoScore()
    {
        var field = CreateFieldInPlay();
        var gorf = CreateRollingGorf(field, 3);
        field.Entities.Add(gorf);
        var scoreBefore = field.ScoreBoard.Score;

        var ticks = RunUntilGone(field, gorf);

        Assert.False(gorf.IsAlive());
        Assert.InRange(ticks, 1, 2999);
        Assert.True(gorf.Position.X >= field.GetPlayfieldBounds().Right -
            ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns));
        Assert.Equal(scoreBefore, field.ScoreBoard.Score);
    }

    /// <summary>Builds a field, puts a Gorf with these rolls on it, and runs it right across.</summary>
    private static (PlayField Field, Gorf Gorf) RunAGorfAcross(LevelParameters parameters, int maxDropsX2,
        params int[] rolls)
    {
        var field = CreateFieldInPlay(parameters);
        foreach (var grunt in
                 field.Entities.Grunts
                     .ToList()) grunt.Kill(); // the wave's own grunts: only the ones Gorf drops are counted

        var gorf = new Gorf(TestSprites.Shared, new ScriptedRandom(rolls), field.GetPlayfieldBounds(), maxDropsX2);
        field.Entities.Add(gorf);
        RunUntilGone(field, gorf);
        return (field, gorf);
    }

    [Fact]
    public void ItStopsThreeTimesOnTheWay_EvenlySpaced()
    {
        var field = CreateFieldInPlay();

        Assert.Equal(GorfTuning.DropStops, CreateGorf(field, 0, 0).GetDropStopsRemaining());
        Assert.Equal(3, GorfTuning.DropStops);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(9, 5)]
    [InlineData(11, 6)]
    public void EachStopDropsHalfTheRollRoundedUp_AsASpheroidRollsItsEnforcers_UpToSix(int roll, int expected)
    {
        // The field has no grunts of its own and a cap of 30, so nothing holds the drops back.
        var (field, _) = RunAGorfAcross(new LevelParameters(1, 30), 12, 0, 0, roll, roll, roll);

        Assert.Equal(3 * expected, field.Entities.Grunts.GetLiveCount());
    }

    [Fact]
    public void ABiggerWaveBoundCanDropMoreAtOnceThanASmallOne()
    {
        // A bound of 4 never gives more than 2 (a roll of 4 halved and rounded up); a bound of 12 can give 6.
        var (small, _) = RunAGorfAcross(new LevelParameters(1, 30), 4, 0, 0, 3, 3, 3);
        var (big, _) = RunAGorfAcross(new LevelParameters(1, 30), 12, 0, 0, 11, 11, 11);

        Assert.Equal(6, small.Entities.Grunts.GetLiveCount());
        Assert.Equal(18, big.Entities.Grunts.GetLiveCount());
    }

    [Fact]
    public void ALevelOnlyHoldsSoManyGrunts_SoAFullOneGetsFewerOrNone()
    {
        // The wave's grunt count is 2, but a level always holds at least six: three stops of six drop one burst and no more.
        var (field, _) = RunAGorfAcross(new LevelParameters(1, 2), 12, 0, 0, 11, 11, 11);

        Assert.Equal(GorfTuning.MinimumGruntCap, field.Entities.Grunts.GetLiveCount());
    }

    [Fact]
    public void ADroppedGruntFallsFromGorfToTheGround_WithNoAppearEffect()
    {
        var field = CreateFieldInPlay();
        var gorf = new Gorf(TestSprites.Shared, new ScriptedRandom(0, 0, 0, 0, 0), field.GetPlayfieldBounds(), 10);
        field.Entities.Add(gorf);

        for (var tick = 0; tick < 3000 && field.Entities.Grunts.Count == 0; tick++) field.Update(Frame);

        var dropped = field.Entities.Grunts[0];
        Assert.True(dropped.IsFalling());
        Assert.False(field.IsMaterialising(dropped));
        Assert.Equal(0, field.GetPendingAppearCount());
        var startY = dropped.Position.Y;

        var guard = 0;
        while (dropped.IsFalling() && guard++ < 200) field.Update(Frame);

        Assert.False(dropped.IsFalling());
        Assert.True(dropped.Position.Y >= startY);
    }

    [Fact]
    public void AFallingGruntDoesNotWalkUntilItHasLanded()
    {
        var field = CreateFieldInPlay();
        var grunt = new Grunt(TestSprites.Shared, new IntVector2(300, 100), 1, new Random(1));
        grunt.BeginFall(100 + 40);

        for (var tick = 0; tick < 4; tick++) grunt.Update(Frame, field);

        Assert.True(grunt.IsFalling());
        Assert.Equal(300, grunt.Position.X);
        Assert.Equal(100 + 4 * GorfTuning.FallPixelsPerTick, grunt.Position.Y);
    }

    [Fact]
    public void AGorfThatIsShotScoresLikeAGrunt_AndMustBeKilledOrPassToWinTheWave()
    {
        var field = CreateFieldInPlay(new LevelParameters(1, GorfCount: 2));

        Assert.Equal(2, field.Entities.Gorfs.Count);
        Assert.Equal(ScoreValues.Grunt, RobotKinds.GetInfo(RobotKind.Gorf).Score);
        Assert.False(field.IsLevelCleared());

        foreach (var gorf in field.Entities.Gorfs) gorf.Kill();

        Assert.True(field.IsLevelCleared());
    }

    [Fact]
    public void ADeathKeepsTheOnesThatSurvive()
    {
        var field = CreateFieldInPlay(new LevelParameters(1, GorfCount: 3));
        field.Entities.Gorfs[0].Kill();

        Assert.Equal(2, WaveSurvivors.GetFrom(field).GorfCount);
    }

    [Fact]
    public void ItSwapsToTheOtherFrameEveryEightRomFrames_AndBackAgain()
    {
        var field = CreateFieldInPlay();
        var gorf = CreateGorf(field, 0, 0, 0);
        var ticksPerFrame = ArcadeClock.ToPortTicksRoundedUp(GorfTuning.AnimationFrameRomFrames);
        Assert.Equal(0, gorf.AnimationFrameIndex);

        for (var tick = 0; tick < ticksPerFrame; tick++) gorf.Update(Frame, field);

        Assert.Equal(1, gorf.AnimationFrameIndex);

        for (var tick = 0; tick < ticksPerFrame; tick++) gorf.Update(Frame, field);

        Assert.Equal(0, gorf.AnimationFrameIndex);
    }
}
