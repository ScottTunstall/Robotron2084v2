using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     PHASE D humans (ROM RRH11): wave spawn (Mikeys/mommies/daddies counts), bounded
///     table walk, hulk-kill → skull marker, player touch → rescue with the
///     running save count (1000-5000, ROM SAVCNT/PCFLG), wave clear independent
///     of humans, and the rescue count starting at none on every wave (ROM PLINIT).
/// </summary>
public sealed class PlayFieldHumanTests
{
    private static WaveParameters HumanWave(int mom, int daddy, int mikey, int hulks = 0)
    {
        return new WaveParameters(
            1,
            0,
            0,
            mom,
            daddy,
            mikey,
            hulks);
    }

    private static PlayField CreateField(WaveParameters waveParameters)
    {
        return new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(99).Build();
    }

    /// <summary>
    ///     One port tick.
    /// </summary>
    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));
    }

    [Fact]
    public void Constructor_SpawnsTheWaveHumanCounts()
    {
        var playField = CreateField(HumanWave(2, 1, 3));

        Assert.Equal(2, playField.FieldEntities.FamilyList.Members.Count(h => h.Kind == HumanKind.Mommy));
        Assert.Equal(1, playField.FieldEntities.FamilyList.Members.Count(h => h.Kind == HumanKind.Daddy));
        Assert.Equal(3, playField.FieldEntities.FamilyList.Members.Count(h => h.Kind == HumanKind.Mikey));
        // ROM HUMSTV spawn order: Mikeys, then mommies, then daddies — the list order
        // matters (the hulk "last slot" target is the last-spawned member).
        Assert.Equal(HumanKind.Mikey, playField.FieldEntities.FamilyList.Members[0].Kind);
        Assert.Equal(HumanKind.Daddy, playField.FieldEntities.FamilyList.Members[^1].Kind);
    }

    [Fact]
    public void Humans_StayInsideTheField_OverManyTicks()
    {
        var playField = CreateField(HumanWave(2, 2, 2));
        var inner = playField.PlayfieldWall.PlayfieldBounds;

        for (var tick = 0; tick < 600; tick++)
        {
            playField.Update(OneTick());
            foreach (var human in playField.FieldEntities.FamilyList.Members)
            {
                Assert.InRange(human.GetBounds().X, inner.X, inner.Right - human.GetBounds().Width);
                Assert.InRange(human.GetBounds().Y, inner.Y, inner.Bottom - human.GetBounds().Height);
                Assert.True(human.GetBounds().Right <= inner.Right && human.GetBounds().Bottom <= inner.Bottom,
                    $"tick {tick}: human {human.GetBounds()} escapes {inner}");
            }
        }
    }

    [Fact]
    public void Humans_MoveOverTime()
    {
        var playField = CreateField(HumanWave(0, 0, 4));
        var first = playField.FieldEntities.FamilyList.Members[0];
        var firstPosition = first.Position;
        var anyMoved = false;

        for (var tick = 0; tick < 300 && !anyMoved; tick++)
        {
            playField.Update(OneTick());
            anyMoved = first.Position != firstPosition;
        }

        Assert.True(anyMoved, "no human moved in 300 ticks");
    }

    [Fact]
    public void Human_StepCadence_RunsOnTheClockUnitAccumulator()
    {
        // 16 fiftieths of a second = 19.2 port ticks (the period is an author override of the
        // ROM's NAP 8 — see Human.BeatIntervalArcadeFrames and notes §70), so the clock-unit
        // clock fires ceil(19.2k) ticks after the FIRST step: 20, 39, 58, 77, 96 … The
        // truncated PortTicks(16) = 19 fired every 19 — a tick further ahead every five
        // steps. The first step itself is the stagger's last tick (notes §88), which is
        // why the grid is measured from it rather than from the object's creation.
        // The human is driven directly so a wave change cannot swap the object out.
        var playField = CreateField(HumanWave(0, 0, 4));
        for (var tick = 0; tick < 130; tick++) playField.Update(OneTick()); // the robots are held until the game goes live

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        var human = new Human(TestTextures.SharedTextureSet, new IntVector2(inner.Center.X, inner.Center.Y), HumanKind.Mommy,
            new Random(7));

        List<int> starts = new();
        var seen = 0;
        for (var tick = 1; tick <= 400 && starts.Count < 10; tick++)
        {
            human.Update(OneTick(), playField);
            if (human.StepCount > seen)
            {
                seen = human.StepCount;
                starts.Add(tick);
            }
        }

        Assert.Equal(10, starts.Count);
        int[] expected = { 0, 20, 39, 58, 77, 96, 116, 135, 154, 173 }; // ceil(19.2k)
        Assert.Equal(expected, starts.Select(t => t - starts[0]));
    }

    [Fact]
    public void Player_TouchingHuman_RescuesWithFirstBonus()
    {
        var playField = CreateField(HumanWave(0, 0, 1));
        playField.SkipWaveStart();
        var human = playField.FieldEntities.FamilyList.Members[0];
        var scoreBefore = playField.ScoreBoard.Score;
        human.MoveTo(playField.Player.Position);

        playField.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, human.LifeState);
        Assert.Equal(1, playField.RescuesThisLife);
        Assert.Equal(scoreBefore + ScoreValues.RescueBonus(1), playField.ScoreBoard.Score);
        Assert.Empty(playField.FieldEntities.Skulls); // a rescue leaves no skull (ROM: PCFLG path)
    }

    [Fact]
    public void Rescues_KeepRunningCountUntilTheCap()
    {
        var playField = CreateField(HumanWave(0, 0, 9));
        playField.SkipWaveStart();
        foreach (var human in playField.FieldEntities.FamilyList.Members) human.MoveTo(playField.Player.Position);

        var scoreBefore = playField.ScoreBoard.Score;
        playField.Update(new GameTime());

        // ROM: SAVCNT itself is uncapped (INC SAVCNT); only the score lookup
        // caps at 5 (CMPA #5 / BLS → SVITAB index). Nine rescues in one tick pay
        // 1000, 2000, 3000, 4000, then the 5000 cap five times.
        Assert.Equal(9, playField.RescuesThisLife);
        Assert.Equal(scoreBefore + 10000 + 5 * ScoreValues.RescueBonus(5), playField.ScoreBoard.Score);
    }

    [Fact]
    public void SyncInto_HandsLiveScoreAndLivesToThePlayersSlot()
    {
        // The HUD draws the SESSION's slots, and the ROM reads the score, the lives
        // out of the player's data block every time it draws — so the
        // hand-over happens every tick (notes §97). Syncing only at a wave clear
        // left the displayed score stale for the rest of the wave, which is what
        // the attract demo's rescue bonus looked like.
        var playField = CreateField(HumanWave(0, 0, 1));
        playField.SkipWaveStart();
        PlayerSlot playerSlot = new(1, new FakeInputSource(), 3, 1);

        playField.Update(new GameTime());
        playField.SyncInto(playerSlot);
        Assert.Equal(0, playerSlot.Score);
        Assert.Equal(playField.Player.Lives, playerSlot.Lives);

        var human = playField.FieldEntities.FamilyList.Members[0];
        human.MoveTo(playField.Player.Position);
        playField.Update(new GameTime());
        playField.Player.AddLife(); // an earned spare life must reach the HUD too
        playField.SyncInto(playerSlot);

        Assert.Equal(ScoreValues.RescueBonus(1), playerSlot.Score);
        Assert.Equal(playField.Player.Lives, playerSlot.Lives);
    }

    [Fact]
    public void Hulk_Contact_KillsHuman_LeavesSkull_NoScore()

    {
        var playField = CreateField(HumanWave(0, 0, 1, 1));

        // The hulk waits for STATUS (`HULK LDA STATUS WAIT FOR STATUS TO GO`) and the ROM
        // creates its collision process only after the wave-start appear, so no robot can touch
        // a human before the game goes live — run through the start of the wave first (notes §88).
        for (var tick = 0; tick < WaveStartTicks.UntilLive(playField); tick++) playField.Update(OneTick());

        var human = playField.FieldEntities.FamilyList.Members[0];
        var scoreBefore = playField.ScoreBoard.Score;
        human.MoveTo(playField.FieldEntities.Hulks[0].Position);

        playField.Update(OneTick());

        Assert.Equal(EntityLifeState.Dead, human.LifeState);
        Assert.Empty(playField.FieldEntities.FamilyList.Members); // pruned after the update
        Assert.Single(playField.FieldEntities.Skulls);
        Assert.Equal(human.Position, playField.FieldEntities.Skulls[0].Position); // skull at the death spot
        Assert.Equal(0, playField.RescuesThisLife);
        Assert.Equal(scoreBefore, playField.ScoreBoard.Score); // robot kills pay nothing
    }

    [Fact]
    public void SkullMarker_ExpiresAfterItsLinger()
    {
        var playField = CreateField(HumanWave(0, 0, 1, 1));

        for (var tick = 0;
             tick < WaveStartTicks.UntilLive(playField);
             tick++) playField.Update(OneTick()); // the hulk cannot act until the game is live (notes §88)

        var human = playField.FieldEntities.FamilyList.Members[0];
        human.MoveTo(playField.FieldEntities.Hulks[0].Position);
        playField.Update(OneTick());
        Assert.Single(playField.FieldEntities.Skulls);

        for (var tick = 0; tick < ArcadeClock.ToPortTicks(90) - 1; tick++) playField.Update(OneTick());

        Assert.Single(playField.FieldEntities.Skulls); // still up just before the linger ends

        playField.Update(OneTick());
        Assert.Empty(playField.FieldEntities.Skulls); // pruned once expired
    }

    [Fact]
    public void Rescue_LeavesBonusMarker_AtTheRescueSpot_ThatExpires()
    {
        var playField = CreateField(HumanWave(0, 0, 1));
        playField.SkipWaveStart();
        var human = playField.FieldEntities.FamilyList.Members[0];
        human.MoveTo(playField.Player.Position);

        playField.Update(new GameTime());

        Assert.Single(playField.FieldEntities.RescueBonusMarkers);
        Assert.Equal(human.Position, playField.FieldEntities.RescueBonusMarkers[0].Position);

        for (var tick = 0; tick < ArcadeClock.ToPortTicks(60) - 1; tick++) playField.Update(new GameTime());

        Assert.Single(playField.FieldEntities.RescueBonusMarkers); // still up just before the linger ends

        playField.Update(new GameTime());
        Assert.Empty(playField.FieldEntities.RescueBonusMarkers); // pruned once expired
    }

    [Fact]
    public void WaveClear_DoesNotRequireHumansGone()
    {
        var playField = CreateField(HumanWave(1, 1, 1));

        Assert.True(playField.IsWaveCleared()); // no robots → cleared even with humans alive (ROM WVCHEK)
    }

    [Fact]
    public void EveryWaveStartsWithNoRescues_SoTheFirstHumanSavedPaysTheFirstBonus()
    {
        var first = CreateField(HumanWave(0, 0, 1));
        first.SkipWaveStart();
        first.FieldEntities.FamilyList.Members[0].MoveTo(first.Player.Position);
        first.Update(new GameTime());
        Assert.Equal(1, first.RescuesThisLife);

        // ROM PLINIT clears SAVCNT as each wave starts, so the next wave pays 1000 again, not 2000.
        var next = CreateField(HumanWave(0, 0, 1));
        next.SkipWaveStart();
        Assert.Equal(0, next.RescuesThisLife);
        next.FieldEntities.FamilyList.Members[0].MoveTo(next.Player.Position);
        next.Update(new GameTime());

        Assert.Equal(1, next.RescuesThisLife);
        Assert.Equal(ScoreValues.RescueBonus(1), next.ScoreBoard.Score);
    }
}
