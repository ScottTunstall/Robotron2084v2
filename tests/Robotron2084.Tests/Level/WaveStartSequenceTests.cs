using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     The start of a wave (notes §142), from `RRG23.ASM` `PLS0A` to `PLS2` (R5 $2831 to $289A).
///     The arcade sets the wave up with everything held (`LDA #$19 / STA STATUS`, $285B), runs `APPEAR`, and then:
///     <list type="bullet">
///         <item>
///             `PLS1` — the player appears. `APPEAR` makes N + 32 passes a fiftieth of a second apart for the N robots on
///             its robot
///             list (`CMPA #32 / BLS`, $2930; `NAP 1`, $2949), then `NAP 2` ($2953) and `NAP 10` ($295D): fiftieth of a
///             second N + 43.
///             With no robots the loop still makes 33 passes: frame 44. On a brain wave it is `NAP 150,PLS1` ($2869).
///         </item>
///         <item>
///             `PLS2` — the game goes live, `NAP 06` ($287A) and `NAP 4` ($2885) later: `MAKP LSPROC / MAKP COLCHK /
///             CLR STATUS` ($2890 to $289A). Until then the player cannot move or fire (`PLAYRV`, `BITA #$01`), nothing
///             collides with the player (the collision process does not exist), and the robots wait (`ROBOT`, `HULK`:
///             `BITA #$7F`).
///         </item>
///     </list>
/// </summary>
public sealed class WaveStartSequenceTests
{
    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
    }

    private static void Tick(PlayField playField, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) playField.Update(OneTick());
    }

    private static PlayField CreateField(WaveParameters waveParameters, IPlayerInputSource? playerInputSource = null,
        bool playerInvincible = true)
    {
        return new PlayFieldBuilder().WithParameters(waveParameters).WithInput(playerInputSource ?? new FakeInputSource())
            .WithPlayerInvincible(playerInvincible).WithSeed(7).Build();
    }

    [Theory]
    [InlineData(15, 58, 68)] // wave 1's fifteen grunts: 15 + 43, then + 10
    [InlineData(1, 44, 54)] // one robot: the loop's 33 passes
    [InlineData(0, 44, 54)] // no robots: still 33 passes, because the marker is at the end at once
    [InlineData(40, 83, 93)]
    public void AnOrdinaryWave_BringsThePlayerInAfterTheAppearLoop_AndGoesLiveTenFramesLater(int robots,
        int playerAppearArcadeFrames, int liveArcadeFrames)
    {
        var waveStartSequence = new WaveStartSequence(robots, false);

        Assert.Equal(playerAppearArcadeFrames, waveStartSequence.PlayerAppearArcadeFrames);
        Assert.Equal(liveArcadeFrames, waveStartSequence.LiveArcadeFrames);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void ABrainWave_BringsThePlayerInAtFrame150_HoweverManyRobotsThereAre(int robots)
    {
        var waveStartSequence = new WaveStartSequence(robots, true);

        Assert.Equal(150, waveStartSequence.PlayerAppearArcadeFrames);
        Assert.Equal(160, waveStartSequence.LiveArcadeFrames);
    }

    [Fact]
    public void TheSequence_ReachesEachMoment_OnTheTickItsArcadeFrameFallsOn()
    {
        var waveStartSequence = new WaveStartSequence(15, false);
        var playerAppearTick = ArcadeClock.ToPortTicksRoundedUp(58); // 69.6 ticks
        var liveTick = ArcadeClock.ToPortTicksRoundedUp(68); // 81.6 ticks

        for (var tick = 1; tick <= liveTick; tick++)
        {
            waveStartSequence.Update();
            Assert.Equal(tick >= playerAppearTick, waveStartSequence.HasPlayerAppeared());
            Assert.Equal(tick >= liveTick, waveStartSequence.IsLive());
        }

        Assert.Equal(70, playerAppearTick);
        Assert.Equal(82, liveTick);
    }

    [Fact]
    public void TheTickTheGameGoesLive_CountsOnlyThePartOfItThatIsLive()
    {
        // arcade frame 68 is 408 clock units and tick 82 is 410, so 2 units of that tick are live play. A timer
        // that starts when the game goes live (GEXEC's) starts from there, not from the whole tick.
        var waveStartSequence = new WaveStartSequence(15, false);
        for (var tick = 1; tick < 82; tick++)
        {
            waveStartSequence.Update();
            Assert.Equal(0, waveStartSequence.GetLiveClockUnitsThisTick());
        }

        waveStartSequence.Update();
        Assert.Equal(2, waveStartSequence.GetLiveClockUnitsThisTick());

        waveStartSequence.Update();
        Assert.Equal(ArcadeClock.UnitsPerPortTick, waveStartSequence.GetLiveClockUnitsThisTick());
    }

    [Fact]
    public void TheField_CountsOnlyTheRobotsOnTheArcadesRobotList()
    {
        // GETROB: grunts (RRP8), hulks (RRH11) and tanks (RRTK4). Spheroids and quarks are on the object
        // list instead, so APPEAR never walks them and they do not lengthen the start.
        var playField = CreateField(new WaveParameters(1, 3, HulkCount: 2, SpheroidCount: 4, QuarkCount: 1, TankCount: 1));

        Assert.Equal(6 + 43, playField.GetPlayerAppearArcadeFrames());
        Assert.Equal(6 + 53, playField.GetLiveArcadeFrames());
    }

    [Fact]
    public void ThePlayer_IsNotOnTheScreen_UntilItAppears()
    {
        var playField = CreateField(new WaveParameters(1, 15));

        Tick(playField, WaveStartTicks.UntilPlayerAppears(playField) - 1);
        Assert.False(playField.HasPlayerAppeared());

        Tick(playField, 1);
        Assert.True(playField.HasPlayerAppeared());
        Assert.False(playField.IsLive()); // PLS1 is ten fiftieths of a second before PLS2
    }

    [Fact]
    public void ThePlayer_CannotMoveOrFire_UntilTheGameIsLive()
    {
        var playerInputSource = new FakeInputSource(new PlayerInputState(new IntVector2(1, 0), new IntVector2(1, 0), true));
        var playField = CreateField(new WaveParameters(1, 15), playerInputSource);
        var start = playField.Player.Position;

        Tick(playField, WaveStartTicks.UntilLive(playField) - 1);

        Assert.Equal(start, playField.Player.Position);
        Assert.Empty(playField.PlayerLaserSlots.GetActiveLasers());
        Assert.False(playField.IsLive());

        Tick(playField, 1); // CLR STATUS

        Assert.True(playField.IsLive());
        Assert.NotEqual(start, playField.Player.Position);
        Assert.Single(playField.PlayerLaserSlots.GetActiveLasers());
    }

    [Fact]
    public void AFamilyMemberIsNotRescued_UntilTheGameIsLive()
    {
        // The collision process is made at PLS2 (`MAKP COLCHK`, $2895), so a family member that walks into
        // the player before then is not rescued.
        var playField = CreateField(new WaveParameters(1, MikeyCount: 1));
        var mikey = playField.FieldEntities.FamilyList.Members[0];

        for (var tick = 1; tick < WaveStartTicks.UntilLive(playField); tick++)
        {
            mikey.MoveTo(playField.Player.Position);
            playField.Update(OneTick());
        }

        Assert.Equal(0, playField.RescuesThisLife);

        mikey.MoveTo(playField.Player.Position);
        playField.Update(OneTick()); // the game goes live

        Assert.Equal(1, playField.RescuesThisLife);
    }

    [Fact]
    public void ARobotOnThePlayer_DoesNotKill_UntilTheGameIsLive()
    {
        var playField = CreateField(new WaveParameters(1), playerInvincible: false);
        playField.FieldEntities.Grunts.Add(new Grunt(TestSprites.SharedSpriteSet, playField.Player.Position));

        Tick(playField, WaveStartTicks.UntilLive(playField) - 1);
        Assert.Equal(EntityLifeState.Alive, playField.Player.LifeState);

        Tick(playField, 1); // the game goes live
        Assert.Equal(EntityLifeState.Dying, playField.Player.LifeState);
    }

    [Fact]
    public void TheRobots_StandStill_UntilTheGameIsLive()
    {
        var playField = CreateField(new WaveParameters(1, 15));
        IntVector2[] starts = [.. playField.FieldEntities.Grunts.Select(grunt => grunt.Position)];

        Tick(playField, WaveStartTicks.UntilLive(playField) - 1);

        Assert.True(playField.RobotsFrozen());
        Assert.Equal(starts, playField.FieldEntities.Grunts.Select(grunt => grunt.Position));

        Tick(playField, 1);

        Assert.False(playField.RobotsFrozen());
    }
}
