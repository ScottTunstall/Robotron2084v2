using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>The BerzerkRobot: the author's own robot, which moves, dies and kills as a grunt does (notes §138).</summary>
public sealed class BerzerkRobotTests
{
    private static PlayField CreateField(LevelParameters? parameters = null) =>
        new PlayFieldBuilder().WithParameters(parameters ?? new LevelParameters(LevelNumber: 1)).WithSeed(5).Build();

    /// <summary>A field whose robots are free to move: the player's start grace period is over.</summary>
    private static PlayField CreateFieldInPlay()
    {
        PlayField field = CreateField();
        var frame = new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
        for (int tick = 0; tick < 400 && field.RobotsFrozen; tick++)
        {
            field.Update(frame);
        }

        return field;
    }

    private static void Tick(PlayField field, int ticks, params IEntity[] robots)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            foreach (IEntity robot in robots)
            {
                robot.Update(new GameTime(), field);
            }
        }
    }

    [Fact]
    public void ItStepsTowardsThePlayerExactlyAsAGruntDoes()
    {
        PlayField field = CreateFieldInPlay();
        IntVector2 start = new(field.Player.Position.X + 120, field.Player.Position.Y - 90);
        var grunt = new Grunt(TestSprites.Shared, start, moveLimitBeats: 6, random: new Random(11));
        var robot = new BerzerkRobot(TestSprites.Shared, start, moveLimitBeats: 6, random: new Random(11));

        for (int tick = 0; tick < 200; tick++)
        {
            grunt.Update(new GameTime(), field);
            robot.Update(new GameTime(), field);
            Assert.Equal(grunt.Position, robot.Position);
            Assert.Equal(grunt.SteppedThisUpdate, robot.SteppedThisUpdate);
        }

        Assert.NotEqual(start, robot.Position);
    }

    [Fact]
    public void ItStandsStillInTheIdleCycleUntilItFirstMoves_ThenWalks()
    {
        PlayField field = CreateFieldInPlay();
        var robot = new BerzerkRobot(TestSprites.Shared, new IntVector2(field.Player.Position.X + 100, field.Player.Position.Y), moveLimitBeats: 3, random: new Random(2));
        IntVector2 start = robot.Position;

        Tick(field, 1, robot);
        Assert.Equal(start, robot.Position);
        Assert.False(robot.SteppedThisUpdate);

        Tick(field, 100, robot);

        Assert.NotEqual(start, robot.Position);
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
    public void TheWalkFramesPlay_OneTwo_SideToSide_AndOneTwoThreeTwo_UpAndDown(WalkSequence facing, int step, int expectedIndex) =>
        Assert.Equal(expectedIndex, BerzerkRobot.GetWalkFrameIndex(facing, step));

    [Theory]
    [InlineData(50, 10, WalkSequence.Right)]
    [InlineData(-50, 10, WalkSequence.Left)]
    [InlineData(10, 50, WalkSequence.Down)]
    [InlineData(10, -50, WalkSequence.Up)]
    [InlineData(30, 30, WalkSequence.Right)]
    public void ItFacesAlongTheLargerGapToThePlayer(int gapX, int gapY, WalkSequence expected) =>
        Assert.Equal(expected, BerzerkRobot.GetWalkSequenceTowards(new IntVector2(100, 100), new IntVector2(100 + gapX, 100 + gapY)));

    [Fact]
    public void ItIsRegisteredLikeAGrunt_ScoringTheSame_FatalToTouch_AndPutOnTheFieldByTheWave()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, BerzerkRobotCount: 3));

        Assert.Equal(3, field.Entities.BerzerkRobots.Count);
        Assert.Equal(ScoreValues.Grunt, RobotKinds.GetInfo(RobotKind.BerzerkRobot).Score);
        Assert.True(RobotKinds.GetInfo(RobotKind.BerzerkRobot).KillsPlayerOnContact);
    }

    [Fact]
    public void WalkingOntoAnElectrode_KillsTheRobotAndTheElectrode()
    {
        PlayField field = CreateField();
        var electrode = new Electrode(TestSprites.Shared, new IntVector2(field.Player.Position.X + 200, field.Player.Position.Y + 100));
        var robot = new BerzerkRobot(TestSprites.Shared, electrode.Position, random: new Random(1));
        field.Entities.Add(electrode);
        field.Entities.Add(robot);

        field.Update(new GameTime());

        Assert.False(robot.IsAlive());
        Assert.False(electrode.IsAlive());
    }

    [Fact]
    public void TheWaveIsNotWonWhileOneIsLeft_AndADeathKeepsTheOnesThatSurvive()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, BerzerkRobotCount: 3));
        Assert.False(field.IsLevelCleared());

        field.Entities.BerzerkRobots[0].Kill();

        Assert.Equal(2, WaveSurvivors.GetFrom(field).BerzerkRobotCount);

        foreach (BerzerkRobot robot in field.Entities.BerzerkRobots)
        {
            robot.Kill();
        }

        Assert.True(field.IsLevelCleared());
    }
}
