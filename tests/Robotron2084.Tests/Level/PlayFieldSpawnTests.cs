using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

public sealed class PlayFieldSpawnTests
{
    public static readonly Rectangle InnerBounds = PlayFieldBuilder.DefaultBounds;

    [Fact]
    public void Constructor_SpawnsExactlyTheParameterCounts()
    {
        PlayField field = CreateField(4, 2, 2, 1, 6);

        Assert.Equal(6, field.GetElectrodeCount());
        Assert.Equal(4, field.GetGruntCount());
        Assert.Equal(2, field.GetHulkCount());
        Assert.Equal(2, field.GetSpheroidCount());
        Assert.Equal(1, field.GetQuarkCount());
        Assert.Equal(0, field.GetEnforcerCount()); // only dropped by spheroids later
        Assert.Equal(0, field.GetTankCount());     // only dropped by quarks later
    }

    [Fact]
    public void Constructor_NoTwoElectrodesOverlap()
    {
        PlayField field = CreateField(4, 2, 2, 1, 6);
        IReadOnlyList<Electrode> electrodes = field.Electrodes;

        for (int i = 0; i < electrodes.Count; i++)
        {
            for (int j = i + 1; j < electrodes.Count; j++)
            {
                Assert.False(electrodes[i].Bounds.Intersects(electrodes[j].Bounds), $"electrodes {i} and {j} overlap");
            }
        }
    }

    [Fact]
    public void Constructor_NoInitialSpawnOverlapsTheWall()
    {
        PlayField field = CreateField(4, 2, 2, 1, 6);

        AssertEveryEntityIsFullyInsidePlayArea(e => e.Bounds, field.Electrodes);
    }

    [Fact]
    public void Constructor_InitialSpawnsRespectMinimumDistancesFromPlayer()
    {
        PlayField field = CreateField(4, 2, 2, 1, 6);
        IntVector2 playerStart = field.Player.Position;

        AssertAllAreFartherThan(field.Electrodes, playerStart, SpawnTuning.ElectrodeMinDistanceFromPlayer);
        AssertAllAreFartherThan(field.Grunts, playerStart, SpawnTuning.GruntMinDistanceFromPlayer);
        AssertAllAreFartherThan(field.Hulks, playerStart, SpawnTuning.HulkMinDistanceFromPlayer);
        AssertAllAreFartherThan(field.Spheroids, playerStart, SpheroidTuning.MinDistanceFromPlayer);
        // Quarks are EXCLUDED: the ROM ($4B48-4B5A) spawns them on the top or
        // bottom wall with a uniform X — no minimum-distance rule (notes 28).
    }

    [Fact]
    public void Constructor_QuarksSpawnOnTheTopOrBottomWall()
    {
        PlayField field = CreateField(0, 0, 0, 3, 0);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        Assert.Equal(3, field.GetQuarkCount());
        foreach (Quark quark in field.Quarks)
        {
            int bottomEdgeY = bounds.Bottom - quark.Bounds.Height;
            Assert.True(quark.Bounds.Y == bounds.Y || quark.Bounds.Y == bottomEdgeY,
                $"quark at y {quark.Bounds.Y} is not on a wall edge (top {bounds.Y}, bottom {bottomEdgeY})");
            Assert.InRange(quark.Bounds.X, bounds.X, bounds.Right - quark.Bounds.Width);
        }
    }

    private static PlayField CreateField(int grunts, int hulks, int spheroids, int quarks, int electrodes)
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: grunts,
            HulkCount: hulks,
            SpheroidCount: spheroids,
            QuarkCount: quarks,
            ElectrodeCount: electrodes,
            MaxEnforcersPerSpheroid: 2,
            MaxTanksPerQuark: 2,
            EnemySpeedBonus: 0);

        return new PlayFieldBuilder().WithParameters(parameters).WithBounds(InnerBounds).WithSeed(1234).Build();
    }

    private static void AssertAllAreFartherThan<T>(IReadOnlyList<T> entities, IntVector2 playerStart, int minSpecPixels)
        where T : IEntity
    {
        long minSquared = ScreenSize.ToPortPixels(minSpecPixels) * (long)ScreenSize.ToPortPixels(minSpecPixels);
        foreach (T entity in entities)
        {
            Assert.True(IntVector2.DistanceSquared(entity.Position, playerStart) > minSquared, $"{typeof(T).Name} spawned too close to the player start");
        }
    }

    private static void AssertEveryEntityIsFullyInsidePlayArea<T>(Func<T, Rectangle> bounds, IReadOnlyList<T> entities)
        where T : IEntity
    {
        // Every entity must be contained in the play area (i.e. not overlap the wall ring).
        foreach (T entity in entities)
        {
            Rectangle b = bounds(entity);
            Assert.True(InnerBounds.X <= b.X && b.Right <= InnerBounds.Right && InnerBounds.Y <= b.Y && b.Bottom <= InnerBounds.Bottom,
                $"{typeof(T).Name} overlaps the wall: {b}");
        }
    }
}
