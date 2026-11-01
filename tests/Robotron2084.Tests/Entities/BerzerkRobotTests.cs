using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>The BerzerkRobot: the author's own robot, which moves, dies and kills as a grunt does (notes §138).</summary>
public sealed class BerzerkRobotTests
{
    private static PlayField CreateField(WaveParameters? waveParameters = null)
    {
        return new PlayFieldBuilder().WithParameters(waveParameters ?? new WaveParameters(1)).WithSeed(5).Build();
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

    [Fact]
    public void ItStepsTowardsThePlayerExactlyAsAGruntDoes()
    {
        var playField = CreateFieldInPlay();
        IntVector2 start = new(playField.Player.Position.X + 120, playField.Player.Position.Y - 90);
        var grunt = new Grunt(TestTextures.SharedTextureSet, start, new Random(11), 6);
        var berzerkRobot = new BerzerkRobot(TestTextures.SharedTextureSet, start, new Random(11), 6);

        for (var tick = 0; tick < 200; tick++)
        {
            grunt.Update(new GameTime(), playField);
            berzerkRobot.Update(new GameTime(), playField);
            Assert.Equal(grunt.Position, berzerkRobot.Position);
            Assert.Equal(grunt.SteppedThisUpdate, berzerkRobot.SteppedThisUpdate);
        }

        Assert.NotEqual(start, berzerkRobot.Position);
    }

    [Fact]
    public void ItStandsStillInTheIdleCycleUntilItFirstMoves_ThenWalks()
    {
        var playField = CreateFieldInPlay();
        var berzerkRobot = new BerzerkRobot(TestTextures.SharedTextureSet,
            new IntVector2(playField.Player.Position.X + 100, playField.Player.Position.Y), new Random(2), 3);
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
        var playField = CreateField(new WaveParameters(1, BerzerkRobotCount: 3));

        Assert.Equal(3, playField.FieldEntities.BerzerkRobots.Count);
        Assert.Equal(ScoreValues.Grunt, RobotKinds.GetInfo(RobotKind.BerzerkRobot).Score);
        Assert.True(RobotKinds.GetInfo(RobotKind.BerzerkRobot).KillsPlayerOnContact);
    }

    [Fact]
    public void WalkingOntoAnElectrode_KillsTheRobotAndTheElectrode()
    {
        var playField = CreateField();
        playField.SkipWaveStart();
        var electrode = new Electrode(TestTextures.SharedTextureSet,
            new IntVector2(playField.Player.Position.X + 200, playField.Player.Position.Y + 100));
        var berzerkRobot = new BerzerkRobot(TestTextures.SharedTextureSet, electrode.Position, random: new Random(1));
        playField.FieldEntities.Add(electrode);
        playField.FieldEntities.Add(berzerkRobot);

        playField.Update(new GameTime());

        Assert.False(berzerkRobot.IsAlive());
        Assert.False(electrode.IsAlive());
    }

    [Fact]
    public void TheWaveIsNotWonWhileOneIsLeft_AndADeathKeepsTheOnesThatSurvive()
    {
        var playField = CreateField(new WaveParameters(1, BerzerkRobotCount: 3));
        Assert.False(playField.IsWaveCleared());

        playField.FieldEntities.BerzerkRobots[0].Kill();

        Assert.Equal(2, WaveSurvivors.GetFrom(playField).BerzerkRobotCount);

        foreach (var berzerkRobot in playField.FieldEntities.BerzerkRobots) berzerkRobot.Kill();

        Assert.True(playField.IsWaveCleared());
    }
}
