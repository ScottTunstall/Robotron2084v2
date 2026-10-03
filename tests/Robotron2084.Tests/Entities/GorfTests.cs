using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>Gorf: the author's own robot, which stands and flaps between two frames (notes §138.2).</summary>
public sealed class GorfTests
{
    private static PlayField CreateField(LevelParameters? parameters = null) =>
        new PlayFieldBuilder().WithParameters(parameters ?? new LevelParameters(LevelNumber: 1)).WithSeed(5).Build();

    [Fact]
    public void ItStaysWhereItIsPut()
    {
        PlayField field = CreateField();
        var gorf = new Gorf(TestSprites.Shared, new IntVector2(300, 200));

        for (int tick = 0; tick < 300; tick++)
        {
            gorf.Update(new GameTime(), field);
        }

        Assert.Equal(new IntVector2(300, 200), gorf.Position);
    }

    [Fact]
    public void ItSwapsToTheOtherFrameEveryEightRomFrames_AndBackAgain()
    {
        PlayField field = CreateField();
        var gorf = new Gorf(TestSprites.Shared, new IntVector2(300, 200));
        Assert.Equal(2, TestSprites.Shared.GorfAnimationFrames.Length);

        // The field's player start grace period freezes robots, so wait it out first.
        var frame = new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
        for (int tick = 0; tick < 400 && field.RobotsFrozen; tick++)
        {
            field.Update(frame);
        }

        int ticksPerFrame = ArcadeClock.ToPortTicksRoundedUp(8);
        Assert.Equal(0, gorf.AnimationFrameIndex);

        for (int tick = 0; tick < ticksPerFrame; tick++)
        {
            gorf.Update(new GameTime(), field);
        }

        Assert.Equal(1, gorf.AnimationFrameIndex);

        for (int tick = 0; tick < ticksPerFrame; tick++)
        {
            gorf.Update(new GameTime(), field);
        }

        Assert.Equal(0, gorf.AnimationFrameIndex);
    }

    [Fact]
    public void ItIsRegisteredAsAKind_PutOnTheFieldByTheWave_AndMustBeKilledToWinIt()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GorfCount: 2));

        Assert.Equal(2, field.Entities.Gorfs.Count);
        Assert.True(RobotKinds.GetInfo(RobotKind.Gorf).KillsPlayerOnContact);
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
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GorfCount: 3));
        field.Entities.Gorfs[0].Kill();

        Assert.Equal(2, WaveSurvivors.GetFrom(field).GorfCount);
    }
}
