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
        PlayField field = new PlayFieldBuilder().WithParameters(parameters ?? new LevelParameters(LevelNumber: 1)).WithSeed(5).Build();
        for (int tick = 0; tick < 400 && field.RobotsFrozen; tick++)
        {
            field.Update(Frame);
        }

        return field;
    }

    private static Gorf CreateGorf(PlayField field, params int[] rolls) =>
        new(TestSprites.Shared, new ScriptedRandom(rolls), field.PlayfieldBounds, maxDropsX2: 10);

    /// <summary>Runs the field until the Gorf is gone, and counts the ticks it took (or gives up).</summary>
    private static int RunUntilGone(PlayField field, Gorf gorf)
    {
        int ticks = 0;
        while (gorf.IsAlive() && ticks < 3000)
        {
            field.Update(Frame);
            ticks++;
        }

        return ticks;
    }

    [Fact]
    public void TheWaveIsSymmetricAndPeaksAtTheFullHeight_WithoutTrigonometry()
    {
        const int period = 80;
        const int height = 30;

        Assert.Equal(0, GorfPath.GetOffset(0, period, height));
        Assert.Equal(height, GorfPath.GetOffset(period / 4, period, height));
        Assert.Equal(0, GorfPath.GetOffset(period / 2, period, height));
        Assert.Equal(-height, GorfPath.GetOffset(period * 3 / 4, period, height));
        Assert.Equal(0, GorfPath.GetOffset(period, period, height));

        for (int step = 0; step < period; step++)
        {
            int offset = GorfPath.GetOffset(step, period, height);
            Assert.InRange(offset, -height, height);
            Assert.Equal(offset, -GorfPath.GetOffset(step + (period / 2), period, height));
        }
    }

    [Fact]
    public void ItStartsJustOffTheScreenOnARandomSide_AtARandomHeight()
    {
        PlayField field = CreateFieldInPlay();
        Rectangle playfield = field.PlayfieldBounds;

        Gorf fromTheLeft = CreateGorf(field, 0, 0, 0);
        Gorf fromTheRight = CreateGorf(field, 1, 0, 0);

        Assert.True(fromTheLeft.Bounds.Right <= playfield.X);
        Assert.True(fromTheRight.Bounds.X >= playfield.Right);

        Gorf high = CreateGorf(field, 0, 0, 0);
        Gorf low = CreateGorf(field, 0, 50, 0);
        Assert.True(high.Position.Y < low.Position.Y);
        Assert.True(low.Position.Y + low.Bounds.Height <= playfield.Bottom);
    }

    [Fact]
    public void ItCrossesTheWholeScreenAndThenIsGone_WithNoScore()
    {
        PlayField field = CreateFieldInPlay();
        Gorf gorf = CreateGorf(field, 0, 0, 0);
        field.Entities.Add(gorf);
        int scoreBefore = field.Score.Score;

        int ticks = RunUntilGone(field, gorf);

        Assert.False(gorf.IsAlive());
        Assert.InRange(ticks, 1, 2999);
        Assert.True(gorf.Position.X >= field.PlayfieldBounds.Right - ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns));
        Assert.Equal(scoreBefore, field.Score.Score);
    }

    [Fact]
    public void ItDropsGrunts_HalfTheRollRoundedUp_EvenlyAlongItsWay()
    {
        PlayField field = CreateFieldInPlay();

        // A roll of 0 here is a drop bound roll of 1: one grunt. A roll of 9 is 10: five grunts.
        Assert.Equal(1, CreateGorf(field, 0, 0, 0).DropsRemaining);
        Assert.Equal(2, CreateGorf(field, 0, 0, 2).DropsRemaining);
        Assert.Equal(5, CreateGorf(field, 0, 0, 9).DropsRemaining);

        Gorf gorf = CreateGorf(field, 0, 0, 9);
        field.Entities.Add(gorf);
        int gruntsBefore = field.Entities.Grunts.Count;
        RunUntilGone(field, gorf);

        Assert.Equal(5, field.Entities.Grunts.Count - gruntsBefore);
    }

    [Fact]
    public void ABiggerWaveBoundDropsMoreGrunts_ThanASmallOne()
    {
        PlayField field = CreateFieldInPlay();
        var top = new Gorf(TestSprites.Shared, new ScriptedRandom(0, 0, 49), field.PlayfieldBounds, maxDropsX2: 50);
        var bottom = new Gorf(TestSprites.Shared, new ScriptedRandom(0, 0, 3), field.PlayfieldBounds, maxDropsX2: 4);

        Assert.Equal(25, top.DropsRemaining);
        Assert.Equal(2, bottom.DropsRemaining);
    }

    [Fact]
    public void AGorfThatIsShotScoresLikeAGrunt_AndMustBeKilledOrPassToWinTheWave()
    {
        PlayField field = CreateFieldInPlay(new LevelParameters(LevelNumber: 1, GorfCount: 2));

        Assert.Equal(2, field.Entities.Gorfs.Count);
        Assert.Equal(ScoreValues.Grunt, RobotKinds.GetInfo(RobotKind.Gorf).Score);
        Assert.False(field.IsLevelCleared());

        foreach (Gorf gorf in field.Entities.Gorfs)
        {
            gorf.Kill();
        }

        Assert.True(field.IsLevelCleared());
    }

    [Fact]
    public void ADeathKeepsTheOnesThatSurvive()
    {
        PlayField field = CreateFieldInPlay(new LevelParameters(LevelNumber: 1, GorfCount: 3));
        field.Entities.Gorfs[0].Kill();

        Assert.Equal(2, WaveSurvivors.GetFrom(field).GorfCount);
    }

    [Fact]
    public void ItSwapsToTheOtherFrameEveryEightRomFrames_AndBackAgain()
    {
        PlayField field = CreateFieldInPlay();
        Gorf gorf = CreateGorf(field, 0, 0, 0);
        int ticksPerFrame = ArcadeClock.ToPortTicksRoundedUp(GorfTuning.AnimationFrameRomFrames);
        Assert.Equal(0, gorf.AnimationFrameIndex);

        for (int tick = 0; tick < ticksPerFrame; tick++)
        {
            gorf.Update(Frame, field);
        }

        Assert.Equal(1, gorf.AnimationFrameIndex);

        for (int tick = 0; tick < ticksPerFrame; tick++)
        {
            gorf.Update(Frame, field);
        }

        Assert.Equal(0, gorf.AnimationFrameIndex);
    }
}
