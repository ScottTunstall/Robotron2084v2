using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// PHASE D humans (ROM RRH11): wave spawn (kids/moms/dads counts), bounded
/// table walk, hulk-kill → skull marker, player touch → rescue with the
/// running save count (1000-5000, ROM SAVCNT/PCFLG), wave clear independent
/// of humans, and the rescue count's lifetime (carries across waves, resets
/// on player death via the PlayField's startingRescues).
/// </summary>
public sealed class PlayFieldHumanTests
{
    private static LevelParameters HumanWave(int mom, int dad, int mikey, int hulks = 0) => new(
        LevelNumber: 1,
        GruntCount: 0,
        ElectrodeCount: 0,
        MomCount: mom,
        DadCount: dad,
        MikeyCount: mikey,
        HulkCount: hulks);

    private static PlayField CreateField(LevelParameters parameters, int startingRescues = 0) =>
        new(parameters, new FakeInputSource(), PlayFieldSpawnTests.InnerBounds, new WallColorCycle(), new Random(99), startingLives: 3, startingRescues: startingRescues);

    /// <summary>
    /// One port tick. The player's start grace is WALL-CLOCK, so a `new GameTime()`
    /// (zero elapsed) leaves `PlayField.RobotsFrozen` true forever and nothing moves.
    /// </summary>
    private static GameTime Frame() => new(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));

    [Fact]
    public void Constructor_SpawnsTheWaveHumanCounts()
    {
        PlayField field = CreateField(HumanWave(2, 1, 3));

        Assert.Equal(2, field.Humans.Count(h => h.Kind == HumanKind.Mom));
        Assert.Equal(1, field.Humans.Count(h => h.Kind == HumanKind.Dad));
        Assert.Equal(3, field.Humans.Count(h => h.Kind == HumanKind.Mikey));
        // ROM HUMSTV spawn order: kids, then moms, then dads — the list order
        // matters (the hulk "last slot" target is the last-spawned member).
        Assert.Equal(HumanKind.Mikey, field.Humans[0].Kind);
        Assert.Equal(HumanKind.Dad, field.Humans[^1].Kind);
    }

    [Fact]
    public void Humans_StayInsideTheField_OverManyTicks()
    {
        PlayField field = CreateField(HumanWave(2, 2, 2));
        Rectangle inner = field.Wall.PlayfieldBounds;

        for (int tick = 0; tick < 600; tick++)
        {
            field.Update(Frame());
            foreach (Human human in field.Humans)
            {
                Assert.InRange(human.Bounds.X, inner.X, inner.Right - human.Bounds.Width);
                Assert.InRange(human.Bounds.Y, inner.Y, inner.Bottom - human.Bounds.Height);
                Assert.True(human.Bounds.Right <= inner.Right && human.Bounds.Bottom <= inner.Bottom, $"tick {tick}: human {human.Bounds} escapes {inner}");
            }
        }
    }

    [Fact]
    public void Humans_MoveOverTime()
    {
        PlayField field = CreateField(HumanWave(0, 0, 4));
        Human first = field.Humans[0];
        IntVector2 firstPosition = first.Position;
        bool anyMoved = false;

        for (int tick = 0; tick < 300 && !anyMoved; tick++)
        {
            field.Update(Frame());
            anyMoved = first.Position != firstPosition;
        }

        Assert.True(anyMoved, "no human moved in 300 ticks");
    }

    [Fact]
    public void Human_StepCadence_RunsOnTheExactSixthsClock()
    {
        // 16 ROM frames = 19.2 port ticks (the period is an author override of the
        // ROM's NAP 8 — see Human.StepPeriodRomTicks and notes §70), so the exact-6ths
        // clock fires ceil(19.2k) ticks after the FIRST step: 20, 39, 58, 77, 96 … The
        // truncated PortTicks(16) = 19 fired every 19 — a tick further ahead every five
        // steps. The first step itself is the stagger's last tick (notes §88), which is
        // why the grid is measured from it rather than from the object's creation.
        // The human is driven directly so a wave change cannot swap the object out.
        PlayField field = CreateField(HumanWave(0, 0, 4));
        for (int tick = 0; tick < 130; tick++)
        {
            field.Update(Frame()); // the player's 2-second start grace freezes the robots
        }

        Rectangle inner = field.Wall.PlayfieldBounds;
        var human = new Human(new IntVector2(inner.Center.X, inner.Center.Y), HumanKind.Mom, new Random(7));

        List<int> starts = new();
        int seen = 0;
        for (int tick = 1; tick <= 400 && starts.Count < 10; tick++)
        {
            human.Update(Frame(), field);
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
        PlayField field = CreateField(HumanWave(0, 0, 1));
        Human human = field.Humans[0];
        int scoreBefore = field.Score.Score;
        human.TeleportTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, human.LifeState);
        Assert.Equal(1, field.RescuesThisLife);
        Assert.Equal(scoreBefore + ScoreValues.RescueBonus(1), field.Score.Score);
        Assert.Empty(field.Skulls); // a rescue leaves no skull (ROM: PCFLG path)
    }

    [Fact]
    public void Rescues_KeepRunningCountUntilTheCap()
    {
        PlayField field = CreateField(HumanWave(0, 0, 5), startingRescues: 4);
        foreach (Human human in field.Humans)
        {
            human.TeleportTo(field.Player.Position);
        }

        int scoreBefore = field.Score.Score;
        field.Update(new GameTime());

        // ROM: SAVCNT itself is uncapped (INC SAVCNT); only the score lookup
        // caps at 5 (CMPA #5 / BLS → SVITAB index). 4 + 5 rescues = 9 saved,
        // and each of the 5 rescues pays the 5000 cap.
        Assert.Equal(9, field.RescuesThisLife);
        Assert.Equal(
            scoreBefore + ScoreValues.RescueBonus(5) + ScoreValues.RescueBonus(5) + ScoreValues.RescueBonus(5) + ScoreValues.RescueBonus(5) + ScoreValues.RescueBonus(5),
            field.Score.Score);
    }

    [Fact]
    public void SyncInto_HandsLiveScoreLivesAndRescuesToThePlayersSlot()
    {
        // The HUD draws the SESSION's slots, and the ROM reads the score, the men
        // and SAVCNT out of the player's data block every time it draws — so the
        // hand-over happens every tick (notes §97). Syncing only at a wave clear
        // left the displayed score stale for the rest of the wave, which is what
        // the attract demo's rescue bonus looked like.
        PlayField field = CreateField(HumanWave(0, 0, 1));
        PlayerSlot slot = new(1, new FakeInputSource(), lives: 3, wave: 1);

        field.Update(new GameTime());
        field.SyncInto(slot);
        Assert.Equal(0, slot.Score);
        Assert.Equal(0, slot.Rescues);
        Assert.Equal(field.Player.Lives, slot.Lives);

        Human human = field.Humans[0];
        human.TeleportTo(field.Player.Position);
        field.Update(new GameTime());
        field.Player.AddLife(); // an earned spare man must reach the HUD too
        field.SyncInto(slot);

        Assert.Equal(ScoreValues.RescueBonus(1), slot.Score);
        Assert.Equal(1, slot.Rescues);
        Assert.Equal(field.Player.Lives, slot.Lives);
        Assert.Equal(field.RescuesThisLife, slot.Rescues);
    }

    [Fact]
    public void Hulk_Contact_KillsHuman_LeavesSkull_NoScore()

    {
        PlayField field = CreateField(HumanWave(0, 0, 1, hulks: 1));

        // The hulk waits for STATUS (`HULK LDA STATUS WAIT FOR STATUS TO GO`) and the ROM
        // creates its collision process only after the wave-start appear, so no robot can touch
        // a human during the player's start grace — burn it off first (notes §88).
        for (int tick = 0; tick < GameplayConstants.PlayerStartGraceSeconds * 60 + 1; tick++)
        {
            field.Update(Frame());
        }

        Human human = field.Humans[0];
        int scoreBefore = field.Score.Score;
        human.TeleportTo(field.Hulks[0].Position);

        field.Update(Frame());

        Assert.Equal(EntityLifeState.Dead, human.LifeState);
        Assert.Empty(field.Humans); // pruned after the update
        Assert.Single(field.Skulls);
        Assert.Equal(human.Position, field.Skulls[0].Position); // skull at the death spot
        Assert.Equal(0, field.RescuesThisLife);
        Assert.Equal(scoreBefore, field.Score.Score); // robot kills pay nothing
    }

    [Fact]
    public void SkullMarker_ExpiresAfterItsLinger()
    {
        PlayField field = CreateField(HumanWave(0, 0, 1, hulks: 1));

        for (int tick = 0; tick < GameplayConstants.PlayerStartGraceSeconds * 60 + 1; tick++)
        {
            field.Update(Frame()); // the hulk cannot act until the grace is over (notes §88)
        }

        Human human = field.Humans[0];
        human.TeleportTo(field.Hulks[0].Position);
        field.Update(Frame());
        Assert.Single(field.Skulls);

        for (int tick = 0; tick < GameplayConstants.PortTicks(90) - 1; tick++)
        {
            field.Update(Frame());
        }

        Assert.Single(field.Skulls); // still up just before the linger ends

        field.Update(Frame());
        Assert.Empty(field.Skulls); // pruned once expired
    }

    [Fact]
    public void Rescue_LeavesScoreDisplay_AtTheRescueSpot_ThatExpires()
    {
        PlayField field = CreateField(HumanWave(0, 0, 1));
        Human human = field.Humans[0];
        human.TeleportTo(field.Player.Position);

        field.Update(new GameTime());

        Assert.Single(field.RescueScores);
        Assert.Equal(human.Position, field.RescueScores[0].Position);

        for (int tick = 0; tick < GameplayConstants.PortTicks(60) - 1; tick++)
        {
            field.Update(new GameTime());
        }

        Assert.Single(field.RescueScores); // still up just before the linger ends

        field.Update(new GameTime());
        Assert.Empty(field.RescueScores); // pruned once expired
    }

    [Fact]
    public void WaveClear_DoesNotRequireHumansGone()
    {
        PlayField field = CreateField(HumanWave(1, 1, 1));

        Assert.True(field.IsLevelCleared); // no robots → cleared even with humans alive (ROM WVCHEK)
    }

    [Fact]
    public void RescueCount_CarriesIntoTheNextField_ViaStartingRescues()
    {
        PlayField first = CreateField(HumanWave(0, 0, 1));
        first.Humans[0].TeleportTo(first.Player.Position);
        first.Update(new GameTime());
        Assert.Equal(1, first.RescuesThisLife);

        PlayField next = CreateField(HumanWave(0, 0, 1), startingRescues: first.RescuesThisLife);
        int baseScore = next.Score.Score; // the next field starts at 0 score; anchor on its own base
        next.Humans[0].TeleportTo(next.Player.Position);
        next.Update(new GameTime());

        Assert.Equal(2, next.RescuesThisLife);
        Assert.Equal(baseScore + ScoreValues.RescueBonus(2), next.Score.Score);
    }
}
