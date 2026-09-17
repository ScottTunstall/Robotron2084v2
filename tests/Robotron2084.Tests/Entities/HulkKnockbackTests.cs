using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
/// Hulk laser knockback (ROM RRH11 HULKIL, decoded 2026-09-13): the hulk is
/// indestructible and is pushed along the laser's direction PER AXIS at ROM
/// magnitudes — X = ±1 arcade px, doubled to ±2 by SEED's sign bit (50%);
/// Y = ±1, quadrupled to ±4 when LSEED &gt;= $C0 (75%) — then clamped at the
/// wall. (Playtest 2026-09-13: the old fixed 20-spec-px push "jumps too far".)
/// All magnitudes here are in internal px (1 arcade px = ScreenSize.Scaled(1)).
/// </summary>
public sealed class HulkKnockbackTests
{
    /// <summary>The field center — far enough from every wall that clamping never interferes.</summary>
    private static IntVector2 Center(PlayField field)
    {
        Rectangle b = field.Wall.PlayfieldBounds;
        int width = ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Width);
        int height = ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Height);
        return new(b.X + b.Width / 2 - width / 2, b.Y + b.Height / 2 - height / 2);
    }

    private static PlayField CreateField()
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
        return new PlayField(
            parameters,
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(1),
            startingLives: 3);
    }

    [Fact]
    public void Hulk_Knockback_UsesOnlyTheRomPerAxisMagnitudes_OnAllEightDirections()
    {
        PlayField field = CreateField();
        IntVector2 spot = Center(field);

        foreach (Direction8 direction in Enum.GetValues<Direction8>())
        {
            var hulk = new Hulk(spot, new Random(1000 + (int)direction), hulkSpeedRomTicks: 2, () => spot);
            field.AddHulk(hulk);
            field.Update(new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3))); // caches the playfield bounds

            IntVector2 unit = direction.ToIntVector();
            var observed = new HashSet<IntVector2>();

            for (int i = 0; i < 400; i++)
            {
                hulk.Position = spot; // reset each hit so the hulk never drifts toward a wall
                hulk.ApplyKnockback(unit);
                observed.Add(hulk.Position - spot);
            }

            int oneX = ScreenSize.Scaled(1);
            int twoX = ScreenSize.Scaled(2);
            int oneY = ScreenSize.Scaled(1);
            int fourY = ScreenSize.Scaled(4);

            // ROM HULKIL: each active axis moves by exactly ±1 or ±2 (X) /
            // ±1 or ±4 (Y) arcade px; the inactive axis never moves.
            Assert.All(
                observed,
                d =>
                {
                    int ax = Math.Abs(d.X);
                    int ay = Math.Abs(d.Y);
                    Assert.True(
                        unit.X == 0 ? ax == 0 : ax == oneX || ax == twoX,
                        $"dx {d.X} for {direction}");
                    Assert.True(
                        unit.Y == 0 ? ay == 0 : ay == oneY || ay == fourY,
                        $"dy {d.Y} for {direction}");
                });

            // 400 seeded hits: the small AND the large magnitude must both
            // appear on every active axis (P(missing one) <= 0.75^400).
            if (unit.X != 0)
            {
                Assert.Contains(unit.X * oneX, observed.Select(d => d.X));
                Assert.Contains(unit.X * twoX, observed.Select(d => d.X));
            }

            if (unit.Y != 0)
            {
                Assert.Contains(unit.Y * oneY, observed.Select(d => d.Y));
                Assert.Contains(unit.Y * fourY, observed.Select(d => d.Y));
            }
        }
    }

    [Fact]
    public void Hulk_Knockback_IsClampedAtTheWall()
    {
        PlayField field = CreateField();
        Rectangle b = field.Wall.PlayfieldBounds;
        int width = ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Width);
        int height = ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Height);

        // Top-left corner, pushed outward (up-left): the hulk may sit on the
        // wall (spec: "pushed back into the WALL") but never leave the field.
        var hulk = new Hulk(new IntVector2(b.X, b.Y), new Random(7), hulkSpeedRomTicks: 2, () => new IntVector2(b.X, b.Y));
        field.AddHulk(hulk);
        field.Update(new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3)));

        for (int i = 0; i < 50; i++)
        {
            hulk.ApplyKnockback(Direction8.UpLeft.ToIntVector());
            Assert.InRange(hulk.Position.X, b.X, b.Right - width);
            Assert.InRange(hulk.Position.Y, b.Y, b.Bottom - height);
        }

        Assert.Equal(new IntVector2(b.X, b.Y), hulk.Position);
    }
}
