using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// ROM-faithful movement tests for the Phase B shell/hulk rewrites
/// (arcade-fidelity-notes §11.5 / RRH11): tank shells fly straight, bounce
/// off all four walls and fizzle after (RND &amp; $1F) + $30 ROM ticks; hulks
/// take one step per HLKSPD-tick cycle.
/// </summary>
public sealed class PlayFieldMovementTests
{
    [Fact]
    public void TankShell_FizzlesWithinRomLifetimeRange_PortTicks48To79RomTicks()
    {
        PlayField field = CreateEmptyField();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        var shell = new TankShell(
            new IntVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
            new IntVector2(1, 0),
            new Random(1234));
        field.AddTankShell(shell);

        int ticks = 0;
        while (shell.LifeState == EntityLifeState.Alive && ticks < 1000)
        {
            shell.Update(new GameTime(), field);
            ticks++;
        }

        Assert.Equal(EntityLifeState.Dead, shell.LifeState);
        // 48..79 ROM frames, held in exact 6ths, so the fizzle lands on
        // ceil(6n/5) — 58..95 port ticks, not the truncated 57..94 (notes §65).
        int minTicks = GameplayConstants.PortTicksCeil(GameplayConstants.TankShellLifeBaseRomTicks);
        int maxTicks = GameplayConstants.PortTicksCeil(GameplayConstants.TankShellLifeBaseRomTicks + 31);
        Assert.InRange(ticks, minTicks, maxTicks);
    }

    [Fact]
    public void TankShell_FliesIntoLeftWall_BouncesAndStaysInsidePlayfield()
    {
        PlayField field = CreateEmptyField();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        // X velocity is -5 plus a -1..1 jitter: always leftward (-6..-4).
        var shell = new TankShell(
            new IntVector2(bounds.X + 80, bounds.Y + bounds.Height / 2),
            new IntVector2(-1, 0),
            new Random(42));
        field.AddTankShell(shell);

        int minX = shell.Position.X;
        for (int i = 0; i < 60 && shell.LifeState == EntityLifeState.Alive; i++)
        {
            shell.Update(new GameTime(), field);
            Assert.True(shell.Bounds.Left >= bounds.Left, $"shell crossed the left wall at tick {i}");
            Assert.True(shell.Bounds.Right <= bounds.Right, $"shell crossed the right wall at tick {i}");
            Assert.True(shell.Bounds.Top >= bounds.Top, $"shell crossed the top wall at tick {i}");
            Assert.True(shell.Bounds.Bottom <= bounds.Bottom, $"shell crossed the bottom wall at tick {i}");
            minX = Math.Min(minX, shell.Position.X);
        }

        // It reached the left wall and came back off it (bounced, not removed).
        Assert.True(shell.Position.X > minX + 8, "shell should have bounced away from the left wall");
    }

    [Fact]
    public void Hulk_StaysPutForOneStepPeriodThenSteps_PortTicksOfHulkSpeed()
    {
        PlayField field = CreateEmptyField();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        // Spawn well away from the player (player-vs-hulk contact is lethal),
        // but hunt the playfield center so the aim is unbounded.
        IntVector2 center = new(bounds.X + bounds.Width / 2 - 16, bounds.Y + bounds.Height / 2 - 16);
        IntVector2 spot = new(bounds.X + 100, bounds.Y + 100);
        var hulk = new Hulk(spot, new Random(7), hulkSpeedRomTicks: 8, () => center);
        field.AddHulk(hulk);

        // End the player's start grace period (robots are frozen during it).
        field.Update(new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3)));
        IntVector2 afterAim = hulk.Position; // first unfrozen update = the spawn aim, no move

        // Step period = 8 ROM frames = 9.6 ticks, so the step lands on the 10th.
        int stepPeriod = GameplayConstants.PortTicksCeil(8);
        for (int i = 1; i < stepPeriod; i++)
        {
            field.Update(new GameTime());
            Assert.Equal(afterAim, hulk.Position);
        }

        field.Update(new GameTime());
        Assert.NotEqual(afterAim, hulk.Position);
        Assert.True(IsFullyInside(hulk.Bounds, bounds));
    }

    [Fact]
    public void Hulk_PressingTheLeftWall_ReaimsAndNeverLeavesPlayfield()
    {
        PlayField field = CreateEmptyField();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        // Right up against the left wall, hunting a point that keeps it aimed left.
        IntVector2 spot = new(bounds.X + 4, bounds.Y + bounds.Height / 2 - 16);
        var hulk = new Hulk(spot, new Random(11), hulkSpeedRomTicks: 5, () => spot);
        field.AddHulk(hulk);

        field.Update(new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3))); // end grace

        IntVector2 positionBefore = hulk.Position;
        for (int i = 0; i < 300; i++)
        {
            field.Update(new GameTime());
            Assert.True(IsFullyInside(hulk.Bounds, bounds));
        }

        Assert.Equal(EntityLifeState.Alive, hulk.LifeState);
        Assert.NotEqual(positionBefore, hulk.Position);
    }

    private static bool IsFullyInside(Rectangle rect, Rectangle bounds) =>
        rect.Left >= bounds.Left && rect.Right <= bounds.Right && rect.Top >= bounds.Top && rect.Bottom <= bounds.Bottom;

    private static PlayField CreateEmptyField()
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

        return new PlayField(parameters, new FakeInputSource(), PlayFieldSpawnTests.InnerBounds, new WallColorCycle(), new Random(99), startingLives: 3);
    }
}
