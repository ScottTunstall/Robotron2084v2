using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// How a tank shell is aimed, how fast it goes, how it bounces and when it fizzles out (RRTK4 <c>TNKFIR</c> and <c>SHELL</c>,
/// R5 <c>$4E46</c> and <c>$4F94</c>).
/// </summary>
public sealed class TankShellTests
{
    private const int ShellSpeed = 176;
    private const int AimedRoll = 0;
    private const int ReboundRoll = 0x80;
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
        Assert.Equal(expectedSpeed, TankShell.ComputeAimedSpeed(gap, ShellSpeed));
    }

    [Theory]
    [InlineData(3, 1, 192, 64)]
    [InlineData(-3, 1, -192, 64)]
    [InlineData(1, 100, 4, 400)]
    [InlineData(500, 1, 500, 1)]
    public void AReboundShotIsDoubledUntilItIsFastEnoughOnEitherAxis(int sideways, int upAndDown, int expectedSideways, int expectedUpAndDown)
    {
        // With a setting of 176 the floor is 176 sideways and 353 up and down (R5 $4F3F to $4F7E).
        IntVector2 boosted = TankShell.GetBoostedVelocity(new IntVector2(sideways, upAndDown), ShellSpeed);
        Assert.Equal(new IntVector2(expectedSideways, expectedUpAndDown), boosted);
    }

    [Fact]
    public void TanksStopFiringOnceTheShellCountIsOverTwenty_AndOnlyALaserKillGivesAShellBack()
    {
        PlayField field = new PlayFieldBuilder().Build();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 200, bounds.Y + 100);

        // CMPA #20 / LBHI: a count of 20 still fires, so the twenty-first shell is the last (R5 $4E57 to $4E5F).
        int fired = 0;
        while (field.CanFireShell() && fired < 100)
        {
            field.SpawnTankShell(tank);
            fired++;
        }

        Assert.Equal(SpawnTuning.ShellCountLimit + 1, fired);

        // A shell that fizzles out does not give its place back (SHELL never does DEC SHLCNT).
        for (int tick = 0; tick < 1000 && field.Entities.TankShells.Any(shell => shell.IsAlive()); tick++)
        {
            foreach (TankShell shell in field.Entities.TankShells.ToList())
            {
                shell.Update(new GameTime(), field);
            }
        }

        Assert.DoesNotContain(field.Entities.TankShells, shell => shell.IsAlive());
        Assert.False(field.CanFireShell());

        // A laser kill does (SHLKIL, R5 $4FD9).
        field.CountShellDestroyed();
        Assert.True(field.CanFireShell());
    }

    [Fact]
    public void WithTheTankShellBugSwitchedOff_AShellThatIsGoneGivesItsPlaceBack()
    {
        PlayField field = new PlayFieldBuilder().WithTankShellBug(false).Build();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 200, bounds.Y + 100);

        int fired = 0;
        while (field.CanFireShell() && fired < 100)
        {
            field.SpawnTankShell(tank);
            fired++;
        }

        // The limit on how many are out at once is still the ROM's.
        Assert.Equal(SpawnTuning.ShellCountLimit + 1, fired);

        for (int tick = 0; tick < 1000 && field.Entities.TankShells.Any(shell => shell.IsAlive()); tick++)
        {
            foreach (TankShell shell in field.Entities.TankShells.ToList())
            {
                shell.Update(new GameTime(), field);
            }
        }

        Assert.True(field.CanFireShell());
    }

    [Fact]
    public void AnAimedShotAtTheSpotItStartsOn_DoesNotMove_AndFizzlesAfterFortyEightTurnsOfTwoRomFrames()
    {
        PlayField field = new PlayFieldBuilder().Build();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 200, bounds.Y + 100);
        IntVector2 player = tank + new IntVector2(ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns), 0);
        var shell = new TankShell(TestSprites.Shared, tank, player, ShellSpeed, bounds, new ScriptedRandom(AimedRoll, NoMiss, NoMiss, 0));

        int ticks = 0;
        while (shell.IsAlive() && ticks < 1000)
        {
            shell.Update(new GameTime(), field);
            ticks++;
        }

        Assert.Equal(0, shell.VelocityX);
        Assert.Equal(0, shell.VelocityY);
        Assert.Equal(ArcadeClock.ToPortTicksRoundedUp(TankShellTuning.LifeBaseBeats * TankShellTuning.BeatIntervalRomFrames), ticks);
    }

    [Fact]
    public void AnAimedShotAimsAtThePlayer_WithASpeedThatGrowsWithTheGap()
    {
        PlayField field = new PlayFieldBuilder().Build();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + 100, bounds.Y + 100);
        IntVector2 start = tank + new IntVector2(ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns), 0);
        IntVector2 player = start + new IntVector2(ScreenSize.ToPortPixelsFromColumns(40), 0);

        var shell = new TankShell(TestSprites.Shared, tank, player, ShellSpeed, bounds, new ScriptedRandom(AimedRoll, NoMiss, NoMiss, 0));

        Assert.Equal(216, shell.VelocityX);
        Assert.Equal(0, shell.VelocityY);
    }

    [Fact]
    public void AReboundShotGoesForTheWallOnThePlayersSide_SevenTimesInEight()
    {
        PlayField field = new PlayFieldBuilder().Build();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 tank = new(bounds.X + (bounds.Width * 3 / 4), bounds.Y + 100);
        IntVector2 playerOnTheLeft = new(bounds.X + (bounds.Width / 4), bounds.Y + 100);

        // Not the top-or-bottom kind (a coin of 0), the near side (a roll of 1), no miss.
        var nearSide = new TankShell(TestSprites.Shared, tank, playerOnTheLeft, ShellSpeed, bounds, new ScriptedRandom(ReboundRoll, 0, 1, NoMiss, 0));
        var farSide = new TankShell(TestSprites.Shared, tank, playerOnTheLeft, ShellSpeed, bounds, new ScriptedRandom(ReboundRoll, 0, TankShellTuning.ReboundFarSideRoll, NoMiss, 0));

        Assert.True(nearSide.VelocityX < 0, "a player on the left sends the shell to the left wall");
        Assert.True(farSide.VelocityX > 0, "the one roll in eight sends it to the far wall");
    }

    [Fact]
    public void AShellThatReachesAWall_BouncesBackOnItsOwnAxis_AndStaysInsideThePlayfield()
    {
        PlayField field = new PlayFieldBuilder().Build();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int boxWidth = ScreenSize.ToPortPixels(CollisionSizes.TankShellCollisionSize.Width);

        // 12 port pixels from the right wall, aimed at a spot 20 columns further right: it flies right, then must bounce.
        int startOffset = ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns);
        IntVector2 tank = new(bounds.Right - boxWidth - 12 - startOffset, bounds.Y + 100);
        IntVector2 start = tank + new IntVector2(startOffset, 0);
        IntVector2 player = start + new IntVector2(ScreenSize.ToPortPixelsFromColumns(20), 0);
        var shell = new TankShell(TestSprites.Shared, tank, player, ShellSpeed, bounds, new ScriptedRandom(AimedRoll, NoMiss, NoMiss, TankShellTuning.LifeExtraBeatsMaxExclusive - 1));
        int sidewaysBefore = shell.VelocityX;
        Assert.True(sidewaysBefore > 0);

        int bounces = 0;
        bool lastTickBounced = false;
        for (int tick = 0; tick < 200 && shell.IsAlive(); tick++)
        {
            shell.Update(new GameTime(), field);
            Assert.True(shell.Bounds.Right <= bounds.Right, $"the shell crossed the right wall on tick {tick}");
            Assert.False(lastTickBounced && shell.BouncedThisUpdate, "a bounce was reported on two ticks running");
            lastTickBounced = shell.BouncedThisUpdate;
            if (shell.BouncedThisUpdate)
            {
                bounces++;
                Assert.Equal(~sidewaysBefore, shell.VelocityX);
                Assert.Equal(0, shell.VelocityY);
                break;
            }
        }

        Assert.Equal(1, bounces);
    }
}
