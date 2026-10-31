using Microsoft.Xna.Framework;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     The player's DEATH — RRX7.ASM's `PDTHV` (notes §66), which replaced the port's
///     wall-clock 2-second placeholder:
///     1. a solid-colour FLASH loop — `$99` (palette index 9) for 2 frames, then a random
///     PDCTAB colour (`$00,$11,$33,$77` → palette indices 0/1/3/7) for 6 frames, ten times
///     (`LDA #10`), i.e. 80 fiftieths of a second;
///     2. the palette index 12 FADE — the DECAY process is stopped, the player keeps being
///     drawn solid in palette index 12, and `FF F6 AD A4 5B 52 09 00` is written into palette index
///     12 a byte per 4 frames; the trailing `$00` ends the death.
///     On the clock-unit clock that is 129.6 port ticks: the flash runs to tick 95,
///     the eight fade writes land on ticks 96/101/106/111/116/120/125/130, and the
///     player is Dead on tick 130.
/// </summary>
public sealed class PlayerDeathTests
{
    private static readonly GameTime TickGameTime = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

    private static (PlayField PlayField, GamePalette GamePalette) CreateField()
    {
        var waveParameters = new WaveParameters(
            1,
            0,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 0,
            MaxTanksPerQuark: 0,
            EnemySpeedBonus: 0);

        var gamePalette = new GamePalette();
        var playField = new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(5).WithPalette(gamePalette).Build();
        return (playField, gamePalette);
    }

    [Fact]
    public void TheFlashStartsInPaletteIndexNine_AndThenUsesOnlyPdctabColours()
    {
        var (playField, _) = CreateField();
        var player = playField.Player;
        player.StartDeathForTesting();

        // PDTH0: `LDA #$99 / JSR OPON` — the first thing on screen is the player
        // as a WHITE silhouette.
        Assert.Equal(EntityLifeState.Dying, player.LifeState);
        Assert.Equal(PlayerTuning.PlayerDeathWhitePaletteIndex, player.GetDeathSolidPaletteIndex());

        // The flash is 80 fiftieths of a second (48 clock units each iteration): every palette index seen
        // through it is either $99's palette index 9 or a PDCTAB entry. Tick to 95 — the
        // 96th tick is where the fade takes over.
        int[] allowed = [.. PlayerTuning.PlayerDeathFlashPaletteIndices, PlayerTuning.PlayerDeathWhitePaletteIndex];
        var sawWhite = false;
        var sawColour = false;
        for (var tick = 0; tick < 95; tick++)
        {
            playField.Update(TickGameTime);
            var paletteIndex = player.GetDeathSolidPaletteIndex();
            Assert.Contains(paletteIndex, allowed);
            sawWhite |= paletteIndex == PlayerTuning.PlayerDeathWhitePaletteIndex;
            sawColour |= paletteIndex != PlayerTuning.PlayerDeathWhitePaletteIndex;
        }

        Assert.True(sawWhite && sawColour, "the flash should alternate white and a PDCTAB colour");
        Assert.Equal(EntityLifeState.Dying, player.LifeState);
    }

    [Fact]
    public void TheFadeWritesTheArcadeTableIntoPaletteIndexTwelve_AndStopsTheDecayProcess()
    {
        var (playField, gamePalette) = CreateField();
        var player = playField.Player;
        player.StartDeathForTesting();

        // The flash ends on tick 96, where the fade's FIRST byte goes in.
        for (var tick = 0; tick < 96; tick++) playField.Update(TickGameTime);

        Assert.Equal(EntityLifeState.Dying, player.LifeState);
        Assert.Equal(PlayerTuning.PlayerDeathFadePaletteIndex, player.GetDeathSolidPaletteIndex());
        Assert.True(gamePalette.IsPaletteIndexSuspended(12), "the ROM kills the DECAY process off before the fade");
        Assert.Equal(PlayerTuning.PlayerDeathFadeValues[0], gamePalette.GetPaletteIndexValue(12));

        // The remaining writes are 4 fiftieths of a second apart, which the clock-unit clock puts
        // on ticks 101/106/111/116/120/125/130 — gaps of 5,5,5,5,4,5,5.
        int[] gaps = [5, 5, 5, 5, 4, 5, 5];
        for (var i = 0; i < gaps.Length; i++)
        {
            for (var tick = 0; tick < gaps[i]; tick++) playField.Update(TickGameTime);

            // The last write is the trailing $00 — the death ends with it.
            Assert.Equal(PlayerTuning.PlayerDeathFadeValues[i + 1], gamePalette.GetPaletteIndexValue(12));
        }

        Assert.Equal(EntityLifeState.Dead, player.LifeState);
        Assert.False(gamePalette.IsPaletteIndexSuspended(12), "the colour processes restart when the fade is done");
        Assert.Equal(2, player.Lives); // one life spent
    }

    [Fact]
    public void TheDyingPlayer_StaysVisibleUntilTheFadeEnds()
    {
        // The death is on the TICK clock now, not a TimeSpan: an Update with a
        // huge elapsed time must not shorten it.
        var (playField, _) = CreateField();
        var player = playField.Player;
        player.StartDeathForTesting();

        for (var tick = 0; tick < 129; tick++)
        {
            playField.Update(new GameTime(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)));
            Assert.Equal(EntityLifeState.Dying, player.LifeState);
        }

        playField.Update(TickGameTime);
        Assert.Equal(EntityLifeState.Dead, player.LifeState);
    }
}
