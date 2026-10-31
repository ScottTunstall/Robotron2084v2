using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     How a tank shell is aimed, how fast it goes, how it bounces and when it fizzles out (RRTK4 <c>TNKFIR</c> and
///     <c>SHELL</c>,
///     R5 <c>$4E46</c> and <c>$4F94</c>).
/// </summary>
public sealed class TankShellTests
{
    private const int ShellSpeed = 176;
    private const int AimedRandom = 0;
    private const int ReboundRandom = 0x80;
    private const int NoMiss = 16;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(40, 216)]
    [InlineData(-40, -224)]
    [InlineData(255, 1400)]
    public void AnAimedShotsSpeed_IsTheGapTimesTheShellSettingOver256_TimesEight(int gap, int expectedSpeed)
    {
        // MUL keeps the top byte of gap x setting (40 x 176 / 256 = 27, x 8 = 216). A gap to the left or above is
        // one less than minus that (COMB), so -40 gives -(27 + 1) x 8.
        Assert.Equal(expectedSpeed, TankShell.GetAimedSpeed(gap, ShellSpeed));
    }

    [Theory]
    [InlineData(3, 1, 192, 64)]
    [InlineData(-3, 1, -192, 64)]
    [InlineData(1, 100, 4, 400)]
    [InlineData(500, 1, 500, 1)]
    public void AReboundShotIsDoubledUntilItIsFastEnoughOnEitherAxis(int sideways, int upAndDown, int expectedSideways,
        int expectedUpAndDown)
    {
        // With a setting of 176 the floor is 176 sideways and 353 up and down (R5 $4F3F to $4F7E).
        var boosted = TankShell.GetBoostedVelocity(new IntVector2(sideways, upAndDown), ShellSpeed);
        Assert.Equal(new IntVector2(expectedSideways, expectedUpAndDown), boosted);
    }

    [Fact]
    public void TanksStopFiringOnceTheShellCountIsOverTwenty_AndOnlyALaserKillGivesAShellBack()
    {
        var playField = new PlayFieldBuilder().Build();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 200, bounds.Y + 100);

        // CMPA #20 / LBHI: a count of 20 still fires, so the twenty-first shell is the last (R5 $4E57 to $4E5F).
        var fired = 0;
        while (playField.CanFireShell() && fired < 100)
        {
            playField.SpawnTankShell(tank);
            fired++;
        }

        Assert.Equal(SpawnTuning.MaxShellCount + 1, fired);

        // A shell that fizzles out does not give its place back (SHELL never does DEC SHLCNT).
        for (var tick = 0; tick < 1000 && playField.FieldEntities.TankShells.Any(tankShell => tankShell.IsAlive()); tick++)
            foreach (var tankShell in playField.FieldEntities.TankShells.ToList())
                tankShell.Update(new GameTime(), playField);

        Assert.DoesNotContain(playField.FieldEntities.TankShells, tankShell => tankShell.IsAlive());
        Assert.False(playField.CanFireShell());

        // A laser kill does (SHLKIL, R5 $4FD9).
        playField.CountShellDestroyed();
        Assert.True(playField.CanFireShell());
    }

    [Fact]
    public void WithTheTankShellBugSwitchedOff_AShellThatIsGoneGivesItsPlaceBack()
    {
        var playField = new PlayFieldBuilder().WithTankShellBug(false).Build();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 200, bounds.Y + 100);

        var fired = 0;
        while (playField.CanFireShell() && fired < 100)
        {
            playField.SpawnTankShell(tank);
            fired++;
        }

        // The limit on how many are out at once is still the ROM's.
        Assert.Equal(SpawnTuning.MaxShellCount + 1, fired);

        for (var tick = 0; tick < 1000 && playField.FieldEntities.TankShells.Any(tankShell => tankShell.IsAlive()); tick++)
            foreach (var tankShell in playField.FieldEntities.TankShells.ToList())
                tankShell.Update(new GameTime(), playField);

        Assert.True(playField.CanFireShell());
    }

    [Fact]
    public void AnAimedShotAtTheSpotItStartsOn_DoesNotMove_AndFizzlesAfterFortyEightBeatsOfTwoArcadeFrames()
    {
        var playField = new PlayFieldBuilder().Build();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 200, bounds.Y + 100);
        var player = tank + new IntVector2(ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns), 0);
        var tankShell = new TankShell(TestTextures.SharedTextureSet, tank, player, ShellSpeed, bounds,
            new ScriptedRandom(AimedRandom, NoMiss, NoMiss, 0));

        var ticks = 0;
        while (tankShell.IsAlive() && ticks < 1000)
        {
            tankShell.Update(new GameTime(), playField);
            ticks++;
        }

        Assert.Equal(0, tankShell.VelocityX);
        Assert.Equal(0, tankShell.VelocityY);
        Assert.Equal(
            ArcadeClock.ToPortTicksRoundedUp(TankShellTuning.LifeBaseBeats * TankShellTuning.BeatIntervalArcadeFrames),
            ticks);
    }

    [Fact]
    public void AnAimedShotAimsAtThePlayer_WithASpeedThatGrowsWithTheGap()
    {
        var playField = new PlayFieldBuilder().Build();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 100, bounds.Y + 100);
        var start = tank + new IntVector2(ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns), 0);
        var player = start + new IntVector2(ScreenSize.ToPortPixelsFromColumns(40), 0);

        var tankShell = new TankShell(TestTextures.SharedTextureSet, tank, player, ShellSpeed, bounds,
            new ScriptedRandom(AimedRandom, NoMiss, NoMiss, 0));

        Assert.Equal(216, tankShell.VelocityX);
        Assert.Equal(0, tankShell.VelocityY);
    }

    [Fact]
    public void AReboundShotGoesForTheWallOnThePlayersSide_SevenTimesInEight()
    {
        var playField = new PlayFieldBuilder().Build();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + bounds.Width * 3 / 4, bounds.Y + 100);
        IntVector2 playerOnTheLeft = new(bounds.X + bounds.Width / 4, bounds.Y + 100);

        // Not the top-or-bottom kind (a coin of 0), the near side (a random number of 1), no miss.
        var nearSideTankShell = new TankShell(TestTextures.SharedTextureSet, tank, playerOnTheLeft, ShellSpeed, bounds,
            new ScriptedRandom(ReboundRandom, 0, 1, NoMiss, 0));
        var farSideTankShell = new TankShell(TestTextures.SharedTextureSet, tank, playerOnTheLeft, ShellSpeed, bounds,
            new ScriptedRandom(ReboundRandom, 0, TankShellTuning.ReboundFarSideRandom, NoMiss, 0));

        Assert.True(nearSideTankShell.VelocityX < 0, "a player on the left sends the shell to the left wall");
        Assert.True(farSideTankShell.VelocityX > 0, "the one random number in eight sends it to the far wall");
    }

    [Fact]
    public void AShellThatReachesAWall_BouncesBackOnItsOwnAxis_AndStaysInsideThePlayfield()
    {
        var playField = new PlayFieldBuilder().Build();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        var boxWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.TankShellCollisionSize.Width);

        // 12 port pixels from the right wall, aimed at a spot 20 columns further right: it flies right, then must bounce.
        var startOffset = ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns);
        IntVector2 tank = new(bounds.Right - boxWidth - 12 - startOffset, bounds.Y + 100);
        var start = tank + new IntVector2(startOffset, 0);
        var player = start + new IntVector2(ScreenSize.ToPortPixelsFromColumns(20), 0);
        var tankShell = new TankShell(TestTextures.SharedTextureSet, tank, player, ShellSpeed, bounds,
            new ScriptedRandom(AimedRandom, NoMiss, NoMiss, TankShellTuning.LifeExtraBeatsMaxExclusive - 1));
        var sidewaysBefore = tankShell.VelocityX;
        Assert.True(sidewaysBefore > 0);

        var bounces = 0;
        var lastTickBounced = false;
        for (var tick = 0; tick < 200 && tankShell.IsAlive(); tick++)
        {
            tankShell.Update(new GameTime(), playField);
            Assert.True(tankShell.GetBounds().Right <= bounds.Right, $"the shell crossed the right wall on tick {tick}");
            Assert.False(lastTickBounced && tankShell.BouncedThisUpdate, "a bounce was reported on two ticks running");
            lastTickBounced = tankShell.BouncedThisUpdate;
            if (tankShell.BouncedThisUpdate)
            {
                bounces++;
                Assert.Equal(~sidewaysBefore, tankShell.VelocityX);
                Assert.Equal(0, tankShell.VelocityY);
                break;
            }
        }

        Assert.Equal(1, bounces);
    }
}
