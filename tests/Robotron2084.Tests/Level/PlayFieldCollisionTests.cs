using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

public sealed class PlayFieldCollisionTests
{
    [Fact]
    public void LaserHitsElectrode_ElectrodeStartsDying_LaserIsDead_AndNoScoreIsAwarded()
    {
        PlayField field = CreateEmptyField();
        IntVector2 spot = new(field.Wall.PlayfieldBounds.X + 100, field.Wall.PlayfieldBounds.Y + 100);
        var electrode = new Electrode(TestSprites.Shared, spot);
        field.AddElectrode(electrode);

        // The laser moves 12 px on its first Update before collisions resolve;
        // from just above the electrode's top-left, moving Down, it still
        // overlaps the 10x9 (spec) electrode box.
        IntVector2 laserOrigin = new(spot.X + 6, spot.Y - 12);
        Assert.True(field.PlayerLasers.TryFire(laserOrigin, Direction8.Down, out PlayerLaser? laser));
        Assert.NotNull(laser);

        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dying, electrode.LifeState);
        Assert.Equal(EntityLifeState.Dead, laser!.LifeState);
        // Arcade-fidelity: destroying a post scores NOTHING (no SCORE call in
        // PSTKIL / ELECTRODE_COLLISION_HANDLER - notes 11.3).
        Assert.Equal(0, field.Score.Score); // ScoreValues.Electrode
    }

    [Fact]
    public void ElectrodeLaserKill_ShrivelsForTheRomFrameTimes_ThenDies()
    {
        // RRP8.ASM PKPROC (the POST KILL PROCESS): the post plays its 3 death
        // pictures (PSP1 → PSP2 → PSP3), holding each for its own sleep time —
        // 6, 3, 2 vblanks — and then the image is turned off (DMAOFF). It is a
        // shape collapse, NOT a blink (the old port toggled visibility for 2 s).
        PlayField field = CreateEmptyField();
        var electrode = new Electrode(TestSprites.Shared, new IntVector2(field.Wall.PlayfieldBounds.X + 100, field.Wall.PlayfieldBounds.Y + 100));
        field.AddElectrode(electrode);

        electrode.Kill();
        Assert.Equal(EntityLifeState.Dying, electrode.LifeState);

        // The ROM's sleeps are 6/3/2 → FRAMES, which on the exact-6ths clock are
        // 7.2/3.6/2.4 ticks: the three shrivel steps land on ticks 8, 11 and 14.
        for (int tick = 0; tick < 13; tick++)
        {
            electrode.Update(new GameTime(), field);
            Assert.Equal(EntityLifeState.Dying, electrode.LifeState);
        }

        electrode.Update(new GameTime(), field);
        Assert.Equal(EntityLifeState.Dead, electrode.LifeState);
    }

    [Fact]
    public void ElectrodePictureFamilyIsWaveDependent()
    {
        // RRG23.ASM GTWCOL: the POST IMAGE table is the third of the wave colour
        // tables, indexed by (wave-1) mod 10, with $10 bytes per family of three
        // pictures — so the post's picture is WAVE-DEPENDENT and repeats every
        // 10 waves: offsets $00,$10,$20,$30,$40,$50,$70,$80,$00,$60.
        int[] expectedPerWave = [0, 1, 2, 3, 4, 5, 7, 8, 0, 6];
        for (int wave = 1; wave <= expectedPerWave.Length; wave++)
        {
            Assert.Equal(expectedPerWave[wave - 1], GameplayConstants.PostFamilyForWave(wave));
        }

        // GTWL wraps the wave into 1..10 (`CMPA #9 / BLS / SUBA #10`), so the
        // sequence repeats every 10 waves.
        Assert.Equal(GameplayConstants.PostFamilyForWave(1), GameplayConstants.PostFamilyForWave(11));
        Assert.Equal(GameplayConstants.PostFamilyForWave(10), GameplayConstants.PostFamilyForWave(20));

        // An electrode is WAVE-derived — both its picture family and its colour —
        // and each family owns 3 consecutive pictures (alive + the two shrivel
        // frames) out of the 27 electrode PNGs.
        var electrode = new Electrode(TestSprites.Shared, new IntVector2(100, 100), wave: 3);
        Assert.Equal(2, electrode.FamilyIndex);
        Assert.True((electrode.FamilyIndex * GameplayConstants.PostPicturesPerFamily) + GameplayConstants.PostPicturesPerFamily <= 27);

        // PSTCOL — the second of RRG23's four per-wave tables (`LDA 10,U / STA
        // PSTCOL`): $FF,$EE,$BB,$DD,$EE,$FF,$11,$BB,$DD,$AA. These are PALETTE
        // SLOTS in the arcade's doubled-nibble form ($XX = two pixels of slot X
        // — the video is 4bpp, 2 pixels per byte, and a SOLID fill needs both
        // nibbles equal), so only the low nibble matters. The old reading took
        // them as colour BYTES, which made wave 1 white and wave 3 dark green;
        // the author confirmed the index reading on 2026-09-16 (notes §47).
        byte[] expectedSlot = [0xFF, 0xEE, 0xBB, 0xDD, 0xEE, 0xFF, 0x11, 0xBB, 0xDD, 0xAA];
        for (int wave = 1; wave <= expectedSlot.Length; wave++)
        {
            Assert.Equal(expectedSlot[wave - 1], GameplayConstants.PostSlotByWaveMod10[wave - 1]);
            Assert.Equal(expectedSlot[wave - 1] & 0x0F, GameplayConstants.PostSlotForWave(wave));
        }

        Assert.Equal(0x0F, GameplayConstants.PostSlotForWave(11)); // wraps every 10
        // Wave 1 names slot 15 and wave 3 names slot 11 — both CYCLING slots
        // (10-15), so those waves' posts cycle, like the hardware palette.
        Assert.Equal(15, GameplayConstants.PostSlotForWave(1));
        Assert.Equal(11, GameplayConstants.PostSlotForWave(3));
        Assert.Equal(1, GameplayConstants.PostSlotForWave(7)); // slot 1 = RED in the ROM's own CRTAB
        Assert.Equal(new Electrode(TestSprites.Shared, new IntVector2(0, 0), wave: 1).TintSlot, GameplayConstants.PostSlotForWave(1));
        Assert.True(GameplayConstants.PostSlotForWave(1) >= 10); // 10-15 are the cycling slots
    }

    [Fact]
    public void WallAndLaserCollideSlots_FollowTheRomWaveTables()
    {
        // WALCOL — the FIRST of RRG23's four per-wave tables (`LDA ,U / STA
        // WALCOL`), and LASCOL — the FOURTH (`LDA 30,U / STA LASCOL`, which
        // RRF.ASM names "LASER WALL COLLIDE COLOR"). Both are palette SLOTS in
        // the doubled-nibble form, indexed by (wave-1) mod 10.
        byte[] wall = [0x22, 0x55, 0x11, 0xEE, 0x77, 0x33, 0x44, 0x88, 0x00, 0xCC];
        byte[] laser = [0x99, 0x00, 0x99, 0x66, 0x99, 0x99, 0x99, 0x11, 0xAA, 0x99];

        for (int wave = 1; wave <= wall.Length; wave++)
        {
            Assert.Equal(wall[wave - 1], GameplayConstants.WallSlotByWaveMod10[wave - 1]);
            Assert.Equal(wall[wave - 1] & 0x0F, GameplayConstants.WallSlotForWave(wave));
            Assert.Equal(laser[wave - 1], GameplayConstants.LaserWallSlotByWaveMod10[wave - 1]);
            Assert.Equal(laser[wave - 1] & 0x0F, GameplayConstants.LaserWallSlotForWave(wave));
        }

        // The wall is NOT the port's old hard-coded slot 11, and it is different
        // on (nearly) every wave — that colour change IS the arcade's look.
        Assert.NotEqual(11, GameplayConstants.WallSlotForWave(1));
        Assert.Equal(2, GameplayConstants.WallSlotForWave(1));  // $22 = slot 2 = ORANGE
        Assert.Equal(1, GameplayConstants.WallSlotForWave(3));  // $11 = slot 1 = RED
        Assert.Equal(14, GameplayConstants.WallSlotForWave(4)); // $EE = a CYCLING slot
        Assert.Equal(12, GameplayConstants.WallSlotForWave(10));// $CC = a CYCLING slot
        Assert.True(GameplayConstants.WallSlotForWave(4) >= 10 && GameplayConstants.WallSlotForWave(10) >= 10);

        // Faithful but surprising: wave 9's border is BLACK (slot 0) and wave 2's
        // laser-wall flare is black too — the ROM's own tables mean that.
        Assert.Equal(0, GameplayConstants.WallSlotForWave(9));
        Assert.Equal(0, GameplayConstants.LaserWallSlotForWave(2));

        // The laser-wall flare is NOT the laser's own slot: wave 1 white (slot 9),
        // wave 4 green (slot 6), wave 8 red (slot 1), wave 9 a cycling slot.
        Assert.Equal(9, GameplayConstants.LaserWallSlotForWave(1));
        Assert.Equal(6, GameplayConstants.LaserWallSlotForWave(4));
        Assert.Equal(1, GameplayConstants.LaserWallSlotForWave(8));
        Assert.Equal(10, GameplayConstants.LaserWallSlotForWave(9));

        // Both wrap every 10 waves (the ROM's GTWCOL subtract-10 loop).
        Assert.Equal(GameplayConstants.WallSlotForWave(1), GameplayConstants.WallSlotForWave(11));
        Assert.Equal(GameplayConstants.WallSlotForWave(9), GameplayConstants.WallSlotForWave(19));
        Assert.Equal(GameplayConstants.WallSlotForWave(10), GameplayConstants.WallSlotForWave(20));
        Assert.Equal(GameplayConstants.LaserWallSlotForWave(2), GameplayConstants.LaserWallSlotForWave(12));
    }

    [Fact]
    public void LaserLeavingThePlayfield_LeavesAFlareOnTheWall_ForTwoPortTicks()
    {
        PlayField field = CreateEmptyField();
        LaserSlots slots = field.PlayerLasers;
        Assert.True(slots.TryFire(new IntVector2(field.Wall.PlayfieldBounds.Center.X, field.Wall.PlayfieldBounds.Top + 40), Direction8.Up, out PlayerLaser? laser));
        Assert.NotNull(laser);

        // Fly it up into the top wall.
        int guard = 0;
        while (laser!.LifeState == EntityLifeState.Alive && guard++ < 200)
        {
            field.Update(Tick);
        }

        // The laser is gone and its flare sits in the TOP wall band, dithered
        // (LASDIV — the ROM mixes WALCOL into LASCOL there).
        Assert.Equal(EntityLifeState.Dead, laser.LifeState);
        Assert.Equal(1, field.LaserWallFlareCount);
        PlayField.LaserWallFlare flare = field.LaserWallFlares[0];
        Assert.True(flare.Bounds.Y < field.Wall.PlayfieldBounds.Top, $"flare {flare.Bounds} is not on the top wall band");
        Assert.True(flare.Dithered);

        // It lives 2 ROM frames = 12 sixths, so it survives two ticks and is
        // dropped on the third (the ROM's `NAP 2`).
        field.Update(Tick);
        Assert.Equal(1, field.LaserWallFlareCount);
        field.Update(Tick);
        Assert.Equal(1, field.LaserWallFlareCount);
        field.Update(Tick);
        Assert.Equal(0, field.LaserWallFlareCount);
    }

    [Fact]
    public void AFlareOnAVerticalWall_IsSolid_NotDithered()
    {
        PlayField field = CreateEmptyField();
        Rectangle inner = field.Wall.PlayfieldBounds;

        // A laser that crossed the RIGHT wall: LASDIH — a solid fill.
        field.SpawnLaserWallFlare(new Rectangle(inner.Right - 2, inner.Center.Y, 4, 4), Direction8.Right);
        Assert.Equal(1, field.LaserWallFlareCount);
        PlayField.LaserWallFlare flare = field.LaserWallFlares[0];
        Assert.False(flare.Dithered);
        Assert.True(flare.Bounds.Right > inner.Right, $"flare {flare.Bounds} is not on the right wall band");

        // A diagonal that ran off the same wall is still a vertical-wall death:
        // only the CROSSED EDGE decides, not the travel axis.
        field.SpawnLaserWallFlare(new Rectangle(inner.Right - 2, inner.Center.Y, 4, 4), Direction8.UpRight);
        Assert.False(field.LaserWallFlares[1].Dithered);
    }

    // The round-7 playtest aid (`PlayerInvincibleForTesting`) makes contact a no-op
    // for the AUTHOR's game, so this test builds its field with the aid OFF — which
    // is exactly what the attract demo does: the machine plays by the arcade's rules
    // (notes §97.5).
    [Fact]
    public void PlayerWalksIntoElectrode_BothStartDying()
    {
        PlayField field = CreateEmptyField(playerInvincibleForTesting: false);
        var electrode = new Electrode(TestSprites.Shared, field.Player.Position); // directly on the player
        field.AddElectrode(electrode);

        int livesBefore = field.Player.Lives;
        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dying, electrode.LifeState);
        Assert.Equal(EntityLifeState.Dying, field.Player.LifeState);
        Assert.Equal(livesBefore - 1, field.Player.Lives);
    }

    [Fact]
    public void PlayerContactWithARobot_KillsUnlessThePlaytestAidIsOn()
    {
        // The author's "the collision detection isn't working" in the attract demo:
        // the demo ran on the same playtest aid as their own game, so its player
        // walked through every robot. The aid is per player now and the DEMO clears
        // it (`AttractState`), so contact kills there and only there (notes §97.5).
        PlayField aided = CreateEmptyField();
        aided.AddGrunt(new Grunt(TestSprites.Shared, aided.Player.Position));
        aided.Update(new GameTime());
        Assert.Equal(EntityLifeState.Alive, aided.Player.LifeState);

        PlayField demo = CreateEmptyField(playerInvincibleForTesting: false);
        demo.AddGrunt(new Grunt(TestSprites.Shared, demo.Player.Position));
        demo.Update(new GameTime());
        Assert.Equal(EntityLifeState.Dying, demo.Player.LifeState);
    }

    [Fact]
    public void LaserHitsGrunt_GruntDiesImmediately_AndExplodes()
    {
        PlayField field = CreateEmptyField();
        IntVector2 spot = new(field.Wall.PlayfieldBounds.X + 200, field.Wall.PlayfieldBounds.Y + 100);
        var grunt = new Grunt(TestSprites.Shared, spot);
        field.AddGrunt(grunt);

        Assert.True(field.PlayerLasers.TryFire(spot, Direction8.Down, out PlayerLaser? laser));
        field.Update(new GameTime());

        // RRP8.ASM ROBKIL: `LDA PCFLG / BNE ROBKON / JSR EXST` — a laser kill
        // goes straight to the explosion ("BLOW HIM UP!!!!") with NO Dying
        // (blink) phase. The flash in the ROM is the PLAYER-CONTACT path
        // (ROBKON → DMAON), which this port does not model yet.
        Assert.Equal(EntityLifeState.Dead, grunt.LifeState);
        Assert.Equal(EntityLifeState.Dead, laser!.LifeState);
        Assert.Equal(100, field.Score.Score); // ScoreValues.Grunt
        Assert.Single(field.Explosions);      // the grunt's death visual
    }

    [Fact]
    public void LaserHitsHulk_HulkIsKnockedBackInLaserDirection_NeverKilled()
    {
        PlayField field = CreateEmptyField();
        IntVector2 spot = new(field.Wall.PlayfieldBounds.X + 200, field.Wall.PlayfieldBounds.Y + 200);
        var hulk = new Hulk(TestSprites.Shared, spot, new Random(7), 8, static () => IntVector2.Zero);
        field.AddHulk(hulk);
        IntVector2 positionBefore = hulk.Position;

        // Laser moving Left knocks the hulk left. Spawn it to the RIGHT of the hulk so the
        // first 12 spec-px move still leaves it overlapping the 14x16 (spec) hulk box.
        Assert.True(field.PlayerLasers.TryFire(new IntVector2(spot.X + 32, spot.Y), Direction8.Left, out PlayerLaser? laser));
        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Alive, hulk.LifeState); // indestructible
        Assert.Equal(EntityLifeState.Dead, laser!.LifeState);
        Assert.True(hulk.Position.X < positionBefore.X, "hulk should be pushed in the laser's direction of travel");
    }

    private static readonly GameTime Tick = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

    private static PlayField CreateEmptyField(bool playerInvincibleForTesting = true)
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: 0,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0);

        return new PlayField(TestSprites.Shared, parameters, new FakeInputSource(), PlayFieldSpawnTests.InnerBounds, new WallColorCycle(), new Random(99), startingLives: 3, playerInvincibleForTesting: playerInvincibleForTesting);
    }
}
