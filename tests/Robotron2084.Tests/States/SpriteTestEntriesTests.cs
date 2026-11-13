using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.States.SpriteTest;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The SPRITE TEST page's list: every sprite, animating at its speed in the game.</summary>
public sealed class SpriteTestEntriesTests
{
    private static readonly IReadOnlyList<SpriteTestEntry> Entries = SpriteTestEntries.Create(TestTextures.SharedTextureSet);

    [Theory]
    [InlineData("PLAYER")]
    [InlineData("MOMMY")]
    [InlineData("DADDY")]
    [InlineData("MIKEY")]
    [InlineData("GRUNT")]
    [InlineData("HULK")]
    [InlineData("BRAIN")]
    [InlineData("SPHEROID")]
    [InlineData("ENFORCER")]
    [InlineData("QUARK")]
    [InlineData("TANK")]
    [InlineData("BERZERK ROBOT")]
    [InlineData("GORF")]
    public void TheListHasEveryCharacter(string name)
    {
        Assert.Contains(Entries, entry => entry.Name == name);
    }

    [Fact]
    public void TheAuthorsOwnRobots_TheBerzerkRobotAndGorf_AreLast()
    {
        Assert.Equal(["BERZERK ROBOT", "GORF"], Entries.TakeLast(2).Select(entry => entry.Name));
    }

    [Fact]
    public void TheListHasOneElectrodeForEachShape()
    {
        var shapes = TestTextures.SharedTextureSet.ElectrodeAnimationFrameTextures.Length / WavePaletteTables.ElectrodeAnimationFramesPerVariant;

        Assert.Equal(shapes, Entries.Count(entry => entry.Name.StartsWith("ELECTRODE ", StringComparison.Ordinal)));
    }

    [Fact]
    public void EveryNameIsUniqueAndInTheSmallFontsCharacters()
    {
        Assert.Equal(Entries.Count, Entries.Select(entry => entry.Name).Distinct().Count());
        Assert.All(Entries, entry =>
            Assert.All(entry.Name, character => Assert.True(character == ' ' || ArcadeText.GetGlyphIndex(character) >= 0)));
    }

    [Fact]
    public void EverySpriteHasACycleToStartOn()
    {
        Assert.All(Entries, entry => Assert.Equal(0, new SpriteTestAnimator(entry, new Random(1)).FrameIndex));
    }

    [Fact]
    public void ThePlayerWalksOnlyWhileAWayIsHeld_ChangingFrameAsOftenAsInTheGame()
    {
        var animator = new SpriteTestAnimator(GetEntry("PLAYER"), new Random(1));
        for (var tick = 0; tick < 10; tick++) animator.Tick(null);
        Assert.Equal(0, animator.FrameIndex);

        animator.Tick(Direction8.Left); // turning to the left walk starts it
        Assert.Equal(0, animator.FrameIndex);

        var walk = GetEntry("PLAYER").GetCycle(Direction8.Left)!;
        Assert.Equal(Player.WalkCycleLength, walk.Frames.Count);
        Assert.All(walk.Frames,
            frame => Assert.Equal(ArcadeClock.ToClockUnits(Player.WalkAnimationIntervalArcadeFrames), frame.HoldClockUnits));
    }

    [Fact]
    public void ThePlayerGoingDiagonally_ShowsTheSameWalkAsGoingSideways_SoTheWalkDoesNotStartAgain()
    {
        var player = GetEntry("PLAYER");

        Assert.Same(player.GetCycle(Direction8.Left), player.GetCycle(Direction8.UpLeft));
        Assert.Same(player.GetCycle(Direction8.Right), player.GetCycle(Direction8.DownRight));
        Assert.NotSame(player.GetCycle(Direction8.Up), player.GetCycle(Direction8.Down));
        Assert.Null(player.GetCycle(null));
    }

    [Fact]
    public void AFamilyMemberHasADifferentWalkForEachOfTheEightWays()
    {
        var mommy = GetEntry("MOMMY");

        Assert.Equal(8, Enum.GetValues<Direction8>().Select(direction => mommy.GetCycle(direction)).Distinct().Count());
    }

    [Fact]
    public void TheTanksTreadsRunBackwardsWhileItGoesLeft_AndForwardsOtherwise()
    {
        var tank = GetEntry("TANK");

        Assert.Same(tank.GetCycle(Direction8.Left), tank.GetCycle(Direction8.DownLeft));
        Assert.NotSame(tank.GetCycle(Direction8.Left), tank.GetCycle(Direction8.Right));
        Assert.Same(tank.GetCycle(Direction8.Right), tank.GetCycle(null));
    }

    [Fact]
    public void TheBerzerkRobotWaitsWhenNoWayIsHeld_AndWalksTheWayHeld()
    {
        var berzerkRobot = GetEntry("BERZERK ROBOT");

        Assert.NotNull(berzerkRobot.GetCycle(null));
        Assert.NotSame(berzerkRobot.GetCycle(null), berzerkRobot.GetCycle(Direction8.Left));
        Assert.Equal(TestTextures.SharedTextureSet.BerzerkRobotIdleAnimationFrameTextures.Length, berzerkRobot.GetCycle(null)!.Frames.Count);
    }

    [Theory]
    [InlineData("ENFORCER GROWING", EnforcerTuning.GrowSteps, EnforcerTuning.GrowIntervalArcadeFrames)]
    [InlineData("TANK GROWING", TankTuning.GrowSteps, TankTuning.GrowIntervalArcadeFrames)]
    public void AGrowingRobotShowsEachGrowFrameAsLongAsInTheGame_ThenItsGrownFrame(string name, int growSteps,
        int growIntervalArcadeFrames)
    {
        var frames = GetEntry(name).GetCycle(null)!.Frames;

        Assert.Equal(growSteps + 1, frames.Count);
        Assert.All(frames.Take(growSteps),
            frame => Assert.Equal(ArcadeClock.ToClockUnits(growIntervalArcadeFrames), frame.HoldClockUnits));
    }

    [Fact]
    public void GorfFlapsAsOftenAsInTheGame_WhicheverWayIsHeld()
    {
        var gorf = GetEntry("GORF");
        var cycle = gorf.GetCycle(null)!;

        Assert.False(gorf.IsSteerable);
        Assert.Same(cycle, gorf.GetCycle(Direction8.Left));
        Assert.All(cycle.Frames, frame => Assert.Equal(ArcadeClock.ToClockUnits(GorfTuning.AnimationIntervalArcadeFrames), frame.HoldClockUnits));
    }

    private static SpriteTestEntry GetEntry(string name)
    {
        return Entries.Single(entry => entry.Name == name);
    }
}
