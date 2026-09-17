using Microsoft.Xna.Framework;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The player's DEATH — RRX7.ASM's `PDTHV` (notes §66), which replaced the port's
/// wall-clock 2-second placeholder:
///
/// 1. a solid-colour FLASH loop — `$99` (slot 9) for 2 frames, then a random
///    PDCTAB colour (`$00,$11,$33,$77` → slots 0/1/3/7) for 6 frames, ten times
///    (`LDA #10`), i.e. 80 ROM frames;
/// 2. the slot-12 FADE — the DECAY process is stopped, the player keeps being
///    drawn solid in slot 12, and `FF F6 AD A4 5B 52 09 00` is written into slot
///    12 a byte per 4 frames; the trailing `$00` ends the death.
///
/// On the exact-6ths clock that is 129.6 port ticks: the flash runs to tick 95,
/// the eight fade writes land on ticks 96/101/106/111/116/120/125/130, and the
/// player is Dead on tick 130.
/// </summary>
public sealed class PlayerDeathTests
{
    private static readonly GameTime Tick = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

    private static (PlayField Field, GamePalette Palette) CreateField()
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: 0,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 0,
            MaxTanksPerQuark: 0,
            EnemySpeedBonus: 0);

        var palette = new GamePalette();
        PlayField field = new(
            parameters,
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(5),
            startingLives: 3,
            wallPalette: palette);
        return (field, palette);
    }

    [Fact]
    public void TheFlashStartsInSlotNine_AndThenUsesOnlyPdctabColours()
    {
        (PlayField field, _) = CreateField();
        Player player = field.Player;
        player.StartDeathForTesting();

        // PDTH0: `LDA #$99 / JSR OPON` — the first thing on screen is the player
        // as a WHITE silhouette.
        Assert.Equal(EntityLifeState.Dying, player.LifeState);
        Assert.Equal(GameplayConstants.PlayerDeathWhiteSlot, player.DeathSolidSlot);

        // The flash is 80 ROM frames (48 sixths each iteration): every slot seen
        // through it is either $99's slot 9 or a PDCTAB entry. Tick to 95 — the
        // 96th tick is where the fade takes over.
        int[] allowed = [.. GameplayConstants.PlayerDeathFlashSlots, GameplayConstants.PlayerDeathWhiteSlot];
        bool sawWhite = false;
        bool sawColour = false;
        for (int tick = 0; tick < 95; tick++)
        {
            field.Update(Tick);
            int slot = player.DeathSolidSlot;
            Assert.Contains(slot, allowed);
            sawWhite |= slot == GameplayConstants.PlayerDeathWhiteSlot;
            sawColour |= slot != GameplayConstants.PlayerDeathWhiteSlot;
        }

        Assert.True(sawWhite && sawColour, "the flash should alternate white and a PDCTAB colour");
        Assert.Equal(EntityLifeState.Dying, player.LifeState);
    }

    [Fact]
    public void TheFadeWritesTheRomTableIntoSlotTwelve_AndStopsTheDecayProcess()
    {
        (PlayField field, GamePalette palette) = CreateField();
        Player player = field.Player;
        player.StartDeathForTesting();

        // The flash ends on tick 96, where the fade's FIRST byte goes in.
        for (int tick = 0; tick < 96; tick++)
        {
            field.Update(Tick);
        }

        Assert.Equal(EntityLifeState.Dying, player.LifeState);
        Assert.Equal(GameplayConstants.PlayerDeathFadeSlot, player.DeathSolidSlot);
        Assert.True(palette.IsSlotSuspended(12), "the ROM kills the DECAY process off before the fade");
        Assert.Equal(GameplayConstants.PlayerDeathFadeValues[0], palette.SlotValue(12));

        // The remaining writes are 4 ROM frames apart, which the sixths clock puts
        // on ticks 101/106/111/116/120/125/130 — gaps of 5,5,5,5,4,5,5.
        int[] gaps = [5, 5, 5, 5, 4, 5, 5];
        for (int i = 0; i < gaps.Length; i++)
        {
            for (int tick = 0; tick < gaps[i]; tick++)
            {
                field.Update(Tick);
            }

            // The last write is the trailing $00 — the death ends with it.
            Assert.Equal(GameplayConstants.PlayerDeathFadeValues[i + 1], palette.SlotValue(12));
        }

        Assert.Equal(EntityLifeState.Dead, player.LifeState);
        Assert.False(palette.IsSlotSuspended(12), "the colour processes restart when the fade is done");
        Assert.Equal(2, player.Lives); // one life spent
    }

    [Fact]
    public void TheDyingPlayer_StaysVisibleUntilTheFadeEnds()
    {
        // The death is on the TICK clock now, not a TimeSpan: an Update with a
        // huge elapsed time must not shorten it.
        (PlayField field, _) = CreateField();
        Player player = field.Player;
        player.StartDeathForTesting();

        for (int tick = 0; tick < 129; tick++)
        {
            field.Update(new GameTime(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)));
            Assert.Equal(EntityLifeState.Dying, player.LifeState);
        }

        field.Update(Tick);
        Assert.Equal(EntityLifeState.Dead, player.LifeState);
    }
}
