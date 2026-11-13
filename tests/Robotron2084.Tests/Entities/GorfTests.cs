using Microsoft.Xna.Framework;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>Gorf: the author's own robot, which crosses the screen in a wave dropping grunts (notes §138.2).</summary>
public sealed class GorfTests
{
    private static readonly GameTime OneTickGameTime = new(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));

    private static PlayField CreateFieldInPlay(WaveParameters? waveParameters = null)
    {
        var playField = new PlayFieldBuilder().WithParameters(waveParameters ?? new WaveParameters(1)).WithSeed(5).Build();
        for (var tick = 0; tick < 400 && playField.RobotsFrozen(); tick++) playField.Update(OneTickGameTime);

        return playField;
    }

    /// <summary>A Gorf whose random numbers are chosen: the side, the ground's height and the drops.</summary>
    private static Gorf CreateGorf(PlayField playField, params int[] randomNumbers)
    {
        return new Gorf(TestTextures.SharedTextureSet, new ScriptedRandom(randomNumbers), playField.GetPlayfieldBounds(), 10);
    }

    /// <summary>A Gorf that picks at random as a real game would, for the tests that run it right across.</summary>
    private static Gorf CreateRandomGorf(PlayField playField, int seed)
    {
        return new Gorf(TestTextures.SharedTextureSet, new Random(seed), playField.GetPlayfieldBounds(), 10);
    }

    /// <summary>Runs the field until the Gorf is gone, and counts the ticks it took (or gives up).</summary>
    private static int RunUntilGone(PlayField playField, Gorf gorf)
    {
        var ticks = 0;
        while (gorf.IsAlive() && ticks < 3000)
        {
            playField.Update(OneTickGameTime);
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
        var playField = CreateFieldInPlay();
        var gorf = CreateRandomGorf(playField, 21);
        var groundY = gorf.Position.Y;
        var startX = gorf.Position.X;
        var stepPixels = GorfTuning.StepPixels;
        var highestSeen = 0;
        var peaks = new List<int>();
        var peakOfThisHop = 0;
        var lastLanding = 0;

        for (var tick = 0; tick < 3000 && gorf.IsAlive(); tick++)
        {
            gorf.Update(OneTickGameTime, playField);
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
        var playField = CreateFieldInPlay();
        var playfield = playField.GetPlayfieldBounds();

        var fromTheLeft = CreateGorf(playField, 0, 0, 0);
        var fromTheRight = CreateGorf(playField, 1, 0, 0);

        Assert.True(fromTheLeft.GetBounds().Right <= playfield.X);
        Assert.True(fromTheRight.GetBounds().X >= playfield.Right);

        var high = CreateGorf(playField, 0, 0, 0);
        var low = CreateGorf(playField, 0, 50, 0);
        Assert.True(high.Position.Y < low.Position.Y);
        Assert.True(low.Position.Y + low.GetBounds().Height <= playfield.Bottom);
    }

    [Fact]
    public void ItCrossesTheWholeScreenAndThenIsGone_WithNoScore()
    {
        var playField = CreateFieldInPlay();
        var gorf = CreateRandomGorf(playField, 3);
        playField.FieldEntities.Add(gorf);
        var scoreBefore = playField.ScoreBoard.Score;

        var ticks = RunUntilGone(playField, gorf);

        Assert.False(gorf.IsAlive());
        Assert.InRange(ticks, 1, 2999);
        Assert.True(gorf.Position.X >= playField.GetPlayfieldBounds().Right -
            GorfTuning.StepPixels);
        Assert.Equal(scoreBefore, playField.ScoreBoard.Score);
    }

    /// <summary>Builds a field, puts a Gorf with these random numbers on it, and runs it right across.</summary>
    private static (PlayField PlayField, Gorf Gorf) RunAGorfAcross(WaveParameters waveParameters, int maxDropsX2,
        params int[] randomNumbers)
    {
        return RunAGorfAcrossDroppingExactly(waveParameters, maxDropsX2, 0, randomNumbers);
    }

    private static (PlayField PlayField, Gorf Gorf) RunAGorfAcrossDroppingExactly(WaveParameters waveParameters,
        int maxDropsX2, int gruntDropCount, params int[] randomNumbers)
    {
        var playField = CreateFieldInPlay(waveParameters);
        foreach (var grunt in
                 playField.FieldEntities.Grunts
                     .ToList()) grunt.Kill(); // the wave's own grunts: only the ones Gorf drops are counted

        var gorf = new Gorf(TestTextures.SharedTextureSet, new ScriptedRandom(randomNumbers), playField.GetPlayfieldBounds(), maxDropsX2,
            gruntDropCount);
        playField.FieldEntities.Add(gorf);
        RunUntilGone(playField, gorf);
        return (playField, gorf);
    }

    [Fact]
    public void ItStopsThreeTimesOnTheWay_EvenlySpaced()
    {
        var playField = CreateFieldInPlay();

        Assert.Equal(GorfTuning.DropStops, CreateGorf(playField, 0, 0).GetDropStopsRemaining());
        Assert.Equal(3, GorfTuning.DropStops);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(9, 5)]
    [InlineData(11, 6)]
    public void EachStopDropsHalfTheRandomNumberRoundedUp_AsASpheroidPicksItsEnforcers_UpToSix(int randomNumber, int expected)
    {
        // The field has no grunts of its own and a maximum of 30, so nothing holds the drops back.
        var (playField, _) = RunAGorfAcross(new WaveParameters(1, 30), 12, 0, 0, randomNumber, randomNumber, randomNumber);

        Assert.Equal(3 * expected, playField.FieldEntities.Grunts.Count(entity => !entity.IsDead()));
    }

    [Fact]
    public void ABiggerWaveBoundCanDropMoreAtOnceThanASmallOne()
    {
        // A bound of 4 never gives more than 2 (a random number of 4 halved and rounded up); a bound of 12 can give 6.
        var (small, _) = RunAGorfAcross(new WaveParameters(1, 30), 4, 0, 0, 3, 3, 3);
        var (big, _) = RunAGorfAcross(new WaveParameters(1, 30), 12, 0, 0, 11, 11, 11);

        Assert.Equal(6, small.FieldEntities.Grunts.Count(entity => !entity.IsDead()));
        Assert.Equal(18, big.FieldEntities.Grunts.Count(entity => !entity.IsDead()));
    }

    [Fact]
    public void ThePlayersBoxIsAvoided_SoGorfIsNeverOnACollisionCourseWithThePlayerWhenItAppears()
    {
        var playField = CreateFieldInPlay();
        var bounds = playField.GetPlayfieldBounds();
        var playerBox = playField.Player.GetBounds();
        var bodyHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Height);
        var hop = ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows);

        for (var seed = 0; seed < 300; seed++)
        {
            var gorf = new Gorf(TestTextures.SharedTextureSet, new Random(seed), bounds, 10, 0, 0, playerBox);

            var top = gorf.Position.Y - hop;
            var bottom = gorf.Position.Y + bodyHeight;
            Assert.True(bottom < playerBox.Y || top > playerBox.Bottom, $"seed {seed}: Gorf's level meets the player's");
        }
    }

    [Fact]
    public void WithoutAPlayerToAvoid_GorfCanAppearOnThePlayersLevel()
    {
        var playField = CreateFieldInPlay();
        var bounds = playField.GetPlayfieldBounds();
        var playerBox = playField.Player.GetBounds();
        var bodyHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Height);
        var hop = ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows);

        var meetsThePlayer = Enumerable.Range(0, 300)
            .Select(seed => new Gorf(TestTextures.SharedTextureSet, new Random(seed), bounds, 10))
            .Any(gorf => gorf.Position.Y + bodyHeight >= playerBox.Y && gorf.Position.Y - hop <= playerBox.Bottom);

        Assert.True(meetsThePlayer);
    }

    [Fact]
    public void AWaveOnlyHoldsSoManyGrunts_SoAFullOneGetsFewerOrNone()
    {
        // The wave's grunt count is 2, but a wave always holds at least six: three stops of six drop one burst and no more.
        var (playField, _) = RunAGorfAcross(new WaveParameters(1, 2), 12, 0, 0, 11, 11, 11);

        Assert.Equal(GorfTuning.MinGruntCount, playField.FieldEntities.Grunts.Count(entity => !entity.IsDead()));
    }

    [Fact]
    public void ADroppedGruntFallsFromGorfToTheGround_WithNoAppearEffect()
    {
        var playField = CreateFieldInPlay();
        var gorf = new Gorf(TestTextures.SharedTextureSet, new ScriptedRandom(0, 0, 0, 0, 0), playField.GetPlayfieldBounds(), 10);
        playField.FieldEntities.Add(gorf);

        for (var tick = 0; tick < 3000 && playField.FieldEntities.Grunts.Count == 0; tick++) playField.Update(OneTickGameTime);

        var dropped = playField.FieldEntities.Grunts[0];
        Assert.True(dropped.IsFalling());
        Assert.False(playField.IsMaterialising(dropped));
        Assert.Equal(0, playField.GetPendingAppearCount());
        var startY = dropped.Position.Y;

        var guard = 0;
        while (dropped.IsFalling() && guard++ < 200) playField.Update(OneTickGameTime);

        Assert.False(dropped.IsFalling());
        Assert.True(dropped.Position.Y >= startY);
    }

    [Fact]
    public void AFallingGruntDoesNotWalkUntilItHasLanded()
    {
        var playField = CreateFieldInPlay();
        var grunt = new Grunt(TestTextures.SharedTextureSet, new IntVector2(300, 100), new Random(1), 1);
        grunt.BeginFall(100 + 40);

        for (var tick = 0; tick < 4; tick++) grunt.Update(OneTickGameTime, playField);

        Assert.True(grunt.IsFalling());
        Assert.Equal(300, grunt.Position.X);
        Assert.Equal(100 + 4 * GorfTuning.FallPixelsPerTick, grunt.Position.Y);
    }

    [Fact]
    public void ItSwapsToTheOtherFrameEveryEightArcadeFrames_AndBackAgain()
    {
        var playField = CreateFieldInPlay();
        var gorf = CreateGorf(playField, 0, 0, 0);
        var ticksPerFrame = ArcadeClock.ToPortTicksRoundedUp(GorfTuning.AnimationIntervalArcadeFrames);
        Assert.Equal(0, gorf.AnimationFrameIndex);

        for (var tick = 0; tick < ticksPerFrame; tick++) gorf.Update(OneTickGameTime, playField);

        Assert.Equal(1, gorf.AnimationFrameIndex);

        for (var tick = 0; tick < ticksPerFrame; tick++) gorf.Update(OneTickGameTime, playField);

        Assert.Equal(0, gorf.AnimationFrameIndex);
    }

    [Fact]
    public void AGruntIsNeverDroppedOntoAnElectrode_ItsDropWaitsUntilGorfHasMovedOn()
    {
        var playField = CreateFieldInPlay(new WaveParameters(1, 0));
        var gorf = new Gorf(TestTextures.SharedTextureSet, new Random(3), playField.GetPlayfieldBounds(), 4, 15);
        playField.FieldEntities.Add(gorf);
        var bodyHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Height);
        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var bounds = playField.GetPlayfieldBounds();
        var groundTop = gorf.Position.Y;

        // Electrodes right under the middle of Gorf's way across, so the drops that fall there must wait.
        var electrodes = new List<Electrode>();
        for (var x = bounds.X + bounds.Width / 2 - 60; x < bounds.X + bounds.Width / 2 + 60; x += 16)
        {
            var electrode = new Electrode(TestTextures.SharedTextureSet, new IntVector2(x, groundTop + bodyHeight / 2));
            electrodes.Add(electrode);
            playField.FieldEntities.Add(electrode);
        }

        var known = new HashSet<Grunt>();
        for (var tick = 0; tick < 6000 && (gorf.IsAlive() || playField.FieldEntities.Gorfs.Any(other => other.IsAlive())); tick++)
        {
            playField.Update(OneTickGameTime);
            foreach (var grunt in playField.FieldEntities.Grunts.Where(grunt => known.Add(grunt)))
            {
                var column = new Rectangle(grunt.Position.X, grunt.Position.Y, gruntWidth, groundTop + bodyHeight - grunt.Position.Y);
                Assert.DoesNotContain(electrodes, electrode => electrode.GetBounds().Intersects(column));
            }
        }

        Assert.True(known.Count > 5, "Gorf should still drop grunts, in the clear places");
    }

    [Fact]
    public void TheSpeechLinesGorfCanSayInTheFirstWave_AreAllRealLinesOfTheSpeechVoice()
    {
        Assert.Equal(6, GorfTuning.IntroAppearanceLines.Count);
        Assert.All(GorfTuning.IntroAppearanceLines, line =>
            Assert.Contains(SpeechLinePaths.All, path => Path.GetFileName(path).ToUpperInvariant() == line));
    }

    [Fact]
    public void AGorfGivenALine_SaysItOnceWhenItFirstAppears_AndNeverAgain()
    {
        var playField = CreateFieldInPlay();
        var lines = new List<string>();
        playField.SetLineSayer(lines.Add);
        var gorf = new Gorf(TestTextures.SharedTextureSet, new Random(3), playField.GetPlayfieldBounds(), 10, 0, 0, null,
            "GORFIANDOOM");
        playField.FieldEntities.Add(gorf);
        Assert.Empty(lines); // not before it appears

        for (var tick = 0; tick < 200; tick++) playField.Update(OneTickGameTime);

        Assert.Equal(["GORFIANDOOM"], lines);
    }

    [Fact]
    public void AGorfGivenNoLine_SaysNothing()
    {
        var playField = CreateFieldInPlay();
        var lines = new List<string>();
        playField.SetLineSayer(lines.Add);
        playField.FieldEntities.Add(CreateRandomGorf(playField, 4));

        for (var tick = 0; tick < 200; tick++) playField.Update(OneTickGameTime);

        Assert.Empty(lines);
    }
}
