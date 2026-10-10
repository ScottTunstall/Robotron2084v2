using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The start of a wave (notes §142), from `RRG23.ASM` `PLS0A` to `PLS2` (R5 $2831 to $289A).
/// The arcade sets the wave up with everything held (`LDA #$19 / STA STATUS`, $285B), runs `APPEAR`, and then:
/// <list type="bullet">
/// <item>`PLS1` — the player appears. `APPEAR` makes N + 32 passes a ROM frame apart for the N robots on its robot
/// list (`CMPA #32 / BLS`, $2930; `NAP 1`, $2949), then `NAP 2` ($2953) and `NAP 10` ($295D): ROM frame N + 43.
/// With no robots the loop still makes 33 passes: frame 44. On a brain wave it is `NAP 150,PLS1` ($2869).</item>
/// <item>`PLS2` — the game goes live, `NAP 06` ($287A) and `NAP 4` ($2885) later: `MAKP LSPROC / MAKP COLCHK /
/// CLR STATUS` ($2890 to $289A). Until then the player cannot move or fire (`PLAYRV`, `BITA #$01`), nothing
/// collides with the player (the collision process does not exist), and the robots wait (`ROBOT`, `HULK`:
/// `BITA #$7F`).</item>
/// </list>
/// </summary>
public sealed class WaveStartSequenceTests
{
    private static GameTime Frame() => new(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));

    private static void Tick(PlayField field, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            field.Update(Frame());
        }
    }

    private static PlayField CreateField(LevelParameters parameters, IPlayerInputSource? input = null, bool playerInvincible = true) =>
        new PlayFieldBuilder().WithParameters(parameters).WithInput(input ?? new FakeInputSource()).WithPlayerInvincible(playerInvincible).WithSeed(7).Build();

    [Theory]
    [InlineData(15, 58, 68)] // wave 1's fifteen grunts: 15 + 43, then + 10
    [InlineData(1, 44, 54)] // one robot: the loop's 33 passes
    [InlineData(0, 44, 54)] // no robots: still 33 passes, because the marker is at the end at once
    [InlineData(40, 83, 93)]
    public void AnOrdinaryWave_BringsThePlayerInAfterTheAppearLoop_AndGoesLiveTenFramesLater(int robots, int playerAppearRomFrames, int liveRomFrames)
    {
        var sequence = new WaveStartSequence(robots, isBrainWave: false);

        Assert.Equal(playerAppearRomFrames, sequence.PlayerAppearRomFrames);
        Assert.Equal(liveRomFrames, sequence.LiveRomFrames);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void ABrainWave_BringsThePlayerInAtFrame150_HoweverManyRobotsThereAre(int robots)
    {
        var sequence = new WaveStartSequence(robots, isBrainWave: true);

        Assert.Equal(150, sequence.PlayerAppearRomFrames);
        Assert.Equal(160, sequence.LiveRomFrames);
    }

    [Fact]
    public void TheSequence_ReachesEachMoment_OnTheTickItsRomFrameFallsOn()
    {
        var sequence = new WaveStartSequence(15, isBrainWave: false);
        int playerAppearTick = ArcadeClock.ToPortTicksRoundedUp(58); // 69.6 ticks
        int liveTick = ArcadeClock.ToPortTicksRoundedUp(68); // 81.6 ticks

        for (int tick = 1; tick <= liveTick; tick++)
        {
            sequence.Update();
            Assert.Equal(tick >= playerAppearTick, sequence.HasPlayerAppeared());
            Assert.Equal(tick >= liveTick, sequence.IsLive());
        }

        Assert.Equal(70, playerAppearTick);
        Assert.Equal(82, liveTick);
    }

    [Fact]
    public void TheTickTheGameGoesLive_CountsOnlyThePartOfItThatIsLive()
    {
        // Frame 68 is 408 clock units and tick 82 is 410, so 2 units of that tick are live play. A timer
        // that starts when the game goes live (GEXEC's) starts from there, not from the whole tick.
        var sequence = new WaveStartSequence(15, isBrainWave: false);
        for (int tick = 1; tick < 82; tick++)
        {
            sequence.Update();
            Assert.Equal(0, sequence.GetLiveClockUnitsThisTick());
        }

        sequence.Update();
        Assert.Equal(2, sequence.GetLiveClockUnitsThisTick());

        sequence.Update();
        Assert.Equal(ArcadeClock.UnitsPerPortTick, sequence.GetLiveClockUnitsThisTick());
    }

    [Fact]
    public void TheField_CountsOnlyTheRobotsOnTheArcadesRobotList()
    {
        // GETROB: grunts (RRP8), hulks (RRH11) and tanks (RRTK4). Spheroids and quarks are on the object
        // list instead, so APPEAR never walks them and they do not lengthen the start.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 3, HulkCount: 2, SpheroidCount: 4, QuarkCount: 1, TankCount: 1));

        Assert.Equal(6 + 43, field.GetPlayerAppearRomFrames());
        Assert.Equal(6 + 53, field.GetLiveRomFrames());
    }

    [Fact]
    public void ThePlayer_IsNotOnTheScreen_UntilItAppears()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 15));

        Tick(field, WaveStartTicks.UntilPlayerAppears(field) - 1);
        Assert.False(field.HasPlayerAppeared());

        Tick(field, 1);
        Assert.True(field.HasPlayerAppeared());
        Assert.False(field.IsLive()); // PLS1 is ten ROM frames before PLS2
    }

    [Fact]
    public void ThePlayer_CannotMoveOrFire_UntilTheGameIsLive()
    {
        var input = new FakeInputSource(new PlayerInputState(new IntVector2(1, 0), new IntVector2(1, 0), FireHeld: true));
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 15), input);
        IntVector2 start = field.Player.Position;

        Tick(field, WaveStartTicks.UntilLive(field) - 1);

        Assert.Equal(start, field.Player.Position);
        Assert.Empty(field.PlayerLasers.GetActiveLasers());
        Assert.False(field.IsLive());

        Tick(field, 1); // CLR STATUS

        Assert.True(field.IsLive());
        Assert.NotEqual(start, field.Player.Position);
        Assert.Single(field.PlayerLasers.GetActiveLasers());
    }

    [Fact]
    public void AFamilyMemberIsNotRescued_UntilTheGameIsLive()
    {
        // The collision process is made at PLS2 (`MAKP COLCHK`, $2895), so a family member that walks into
        // the player before then is not rescued.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, MikeyCount: 1));
        Human mikey = field.Entities.Family.Members[0];

        for (int tick = 1; tick < WaveStartTicks.UntilLive(field); tick++)
        {
            mikey.MoveTo(field.Player.Position);
            field.Update(Frame());
        }

        Assert.Equal(0, field.RescuesThisLife);

        mikey.MoveTo(field.Player.Position);
        field.Update(Frame()); // the game goes live

        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void ARobotOnThePlayer_DoesNotKill_UntilTheGameIsLive()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1), playerInvincible: false);
        field.Entities.Grunts.Add(new Grunt(TestSprites.Shared, field.Player.Position));

        Tick(field, WaveStartTicks.UntilLive(field) - 1);
        Assert.Equal(EntityLifeState.Alive, field.Player.LifeState);

        Tick(field, 1); // the game goes live
        Assert.Equal(EntityLifeState.Dying, field.Player.LifeState);
    }

    [Fact]
    public void TheRobots_StandStill_UntilTheGameIsLive()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 15));
        IntVector2[] starts = [.. field.Entities.Grunts.Select(grunt => grunt.Position)];

        Tick(field, WaveStartTicks.UntilLive(field) - 1);

        Assert.True(field.RobotsFrozen());
        Assert.Equal(starts, field.Entities.Grunts.Select(grunt => grunt.Position));

        Tick(field, 1);

        Assert.False(field.RobotsFrozen());
    }
}
