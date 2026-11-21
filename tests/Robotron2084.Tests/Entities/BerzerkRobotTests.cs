using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;
using Robotron2084.Core;
using Robotron2084.Entities.Extensions;
using Robotron2084.Entities.Models;
using Robotron2084.Entities;
using Robotron2084.Events;
using Robotron2084.Level.Models;
using Robotron2084.Level;
using Robotron2084.Score;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>The BerzerkRobot: the author's own robot, which moves, dies and kills as a grunt does (notes §138).</summary>
public sealed class BerzerkRobotTests
{
    private static PlayField CreateField(WaveParams? waveParams = null)
    {
        return new PlayFieldBuilder().WithParameters(waveParams ?? new WaveParams(1)).WithSeed(5).Build();
    }

    /// <summary>A field whose robots are free to move: the start of the wave is over, so the game is live.</summary>
    private static PlayField CreateFieldInPlay()
    {
        var playField = CreateField();
        var frame = new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
        for (var tick = 0; tick < 400 && playField.RobotsFrozen(); tick++) playField.Update(frame);

        return playField;
    }

    private static void Tick(PlayField playField, int ticks, params IEntity[] robots)
    {
        for (var tick = 0; tick < ticks; tick++)
            foreach (var berzerkRobot in robots)
                berzerkRobot.Update(new GameTime(), playField);
    }

    /// <summary>Ticks in one step of the robot: <see cref="BerzerkRobot.StepIntervalBeats" /> beats.</summary>
    private static readonly int StepIntervalTicks =
        ArcadeClock.ToPortTicks(BerzerkRobot.StepIntervalBeats * BerzerkRobot.BeatIntervalArcadeFrames);

    /// <summary>One arcade pixel, in port pixels: how far one step goes.</summary>
    private static readonly int StepPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

    [Fact]
    public void ARobotIsBlue_UnlessItIsGivenAColour()
    {
        var blueRobot = new BerzerkRobot(TestEntityParams.Create(new Random(1)), IntVector2.Zero);
        var greenRobot = new BerzerkRobot(TestEntityParams.Create(new Random(1)), IntVector2.Zero, Color.Lime);

        Assert.Equal(new Color(0, 0, 240), BerzerkRobot.DefaultColour);
        Assert.Equal(BerzerkRobot.DefaultColour, blueRobot.Colour);
        Assert.Equal(Color.Lime, greenRobot.Colour);
    }

    [Fact]
    public void ItDrawsTheStripsOfItsAppearAndExplosion_InItsOwnColour()
    {
        var berzerkRobot = new BerzerkRobot(TestEntityParams.Create(new Random(1)), IntVector2.Zero);

        Assert.IsAssignableFrom<IStripDrawer>(berzerkRobot);
    }

    [Fact]
    public void ItStepsAtASteadyPace_WithNoRandomWait()
    {
        var playField = CreateFieldInPlay();
        IntVector2 start = new(playField.Player.Position.X + 120, playField.Player.Position.Y);
        var berzerkRobot = new BerzerkRobot(TestEntityParams.Create(new Random(11)), start);
        List<int> stepTicks = [];

        for (var tick = 0; tick < 300; tick++)
        {
            berzerkRobot.Update(new GameTime(), playField);
            if (berzerkRobot.SteppedThisUpdate) stepTicks.Add(tick);
        }

        var gapsInTicks = stepTicks.Zip(stepTicks.Skip(1), (earlierTick, laterTick) => laterTick - earlierTick).ToList();
        Assert.True(gapsInTicks.Count > 10);
        Assert.All(gapsInTicks, gapInTicks => Assert.InRange(gapInTicks, StepIntervalTicks, StepIntervalTicks + 1));
        Assert.Equal(start.X - stepTicks.Count * StepPixels, berzerkRobot.Position.X);
    }

    [Theory]
    [InlineData(40, -10, 1, 0)] // further away sideways: steps right
    [InlineData(-40, 10, -1, 0)] // further away sideways, to the left: steps left
    [InlineData(10, 40, 0, 1)] // further away below: steps down
    [InlineData(-10, -40, 0, -1)] // further away above: steps up
    [InlineData(30, 30, 1, 0)] // as far either way: steps sideways, never on a slant
    public void EachStep_IsOnePixelSidewaysOrUpOrDown_NeverOnASlant(int gapX, int gapY, int stepsX, int stepsY)
    {
        IntVector2 position = new(200, 200);
        IntVector2 playerPosition = new(position.X + gapX * StepPixels, position.Y + gapY * StepPixels);

        var nextPosition = BerzerkRobot.GetNextPosition(position, playerPosition, new Rectangle(0, 0, 2000, 2000));

        Assert.Equal(new IntVector2(position.X + stepsX * StepPixels, position.Y + stepsY * StepPixels), nextPosition);
    }

    [Fact]
    public void AStep_NeverGoesPastThePlayersLine()
    {
        IntVector2 position = new(200, 200);
        IntVector2 playerPosition = new(position.X + StepPixels - 1, position.Y);

        Assert.Equal(playerPosition, BerzerkRobot.GetNextPosition(position, playerPosition, new Rectangle(0, 0, 2000, 2000)));
    }

    [Fact]
    public void ItStandsStillInTheIdleCycleUntilItFirstMoves_ThenWalks()
    {
        var playField = CreateFieldInPlay();
        var berzerkRobot = new BerzerkRobot(TestEntityParams.Create(new Random(2)), new IntVector2(playField.Player.Position.X + 100, playField.Player.Position.Y));
        var start = berzerkRobot.Position;

        Tick(playField, 1, berzerkRobot);
        Assert.Equal(start, berzerkRobot.Position);
        Assert.False(berzerkRobot.SteppedThisUpdate);

        Tick(playField, 100, berzerkRobot);

        Assert.NotEqual(start, berzerkRobot.Position);
    }

    [Theory]
    [InlineData(WalkSequence.Right, 0, 0)]
    [InlineData(WalkSequence.Right, 1, 1)]
    [InlineData(WalkSequence.Right, 2, 0)]
    [InlineData(WalkSequence.Left, 3, 1)]
    [InlineData(WalkSequence.Up, 0, 0)]
    [InlineData(WalkSequence.Up, 1, 1)]
    [InlineData(WalkSequence.Up, 2, 2)]
    [InlineData(WalkSequence.Up, 3, 1)]
    [InlineData(WalkSequence.Down, 4, 0)]
    [InlineData(WalkSequence.Down, 7, 1)]
    public void TheWalkFramesPlay_OneTwo_SideToSide_AndOneTwoThreeTwo_UpAndDown(WalkSequence facing, int step,
        int expectedIndex)
    {
        Assert.Equal(expectedIndex, BerzerkRobot.GetWalkFrameIndex(facing, step));
    }

    [Theory]
    [InlineData(50, 10, WalkSequence.Right)]
    [InlineData(-50, 10, WalkSequence.Left)]
    [InlineData(10, 50, WalkSequence.Down)]
    [InlineData(10, -50, WalkSequence.Up)]
    [InlineData(30, 30, WalkSequence.Right)]
    public void ItFacesAlongTheLargerGapToThePlayer(int gapX, int gapY, WalkSequence expected)
    {
        Assert.Equal(expected,
            BerzerkRobot.GetWalkSequenceTowards(new IntVector2(100, 100), new IntVector2(100 + gapX, 100 + gapY)));
    }

    [Fact]
    public void ItIsRegisteredLikeAGrunt_ScoringTheSame_FatalToTouch_AndPutOnTheFieldByTheWave()
    {
        var playField = CreateField(new WaveParams(1, BerzerkRobotCount: 3));

        Assert.Equal(3, playField.FieldEntities.BerzerkRobots.Count);
        Assert.Equal(ScoreValues.Grunt, RobotKinds.GetInfo(RobotKind.BerzerkRobot).Score);
        Assert.True(RobotKinds.GetInfo(RobotKind.BerzerkRobot).KillsPlayerOnContact);
    }

    [Fact]
    public void WalkingOntoAnElectrode_KillsTheRobotAndTheElectrode()
    {
        var playField = CreateField();
        playField.SkipWaveStart();
        var electrode = new Electrode(TestTextures.SharedTextureSet, NullGameEvents.Instance,
            new IntVector2(playField.Player.Position.X + 200, playField.Player.Position.Y + 100));
        var berzerkRobot = new BerzerkRobot(TestEntityParams.Create(new Random(1)), electrode.Position);
        playField.FieldEntities.Electrodes.Add(electrode);
        playField.FieldEntities.BerzerkRobots.Add(berzerkRobot);

        playField.Update(new GameTime());

        Assert.False(berzerkRobot.IsAlive());
        Assert.False(electrode.IsAlive());
    }

    [Fact]
    public void TheWaveIsNotWonWhileOneIsLeft_AndADeathKeepsTheOnesThatSurvive()
    {
        var playField = CreateField(new WaveParams(1, BerzerkRobotCount: 3));
        Assert.False(playField.IsWaveCleared());

        playField.FieldEntities.BerzerkRobots[0].Kill();

        Assert.Equal(2, playField.GetWaveParamsForNextLife().BerzerkRobotCount);

        foreach (var berzerkRobot in playField.FieldEntities.BerzerkRobots) berzerkRobot.Kill();

        Assert.True(playField.IsWaveCleared());
    }
}
