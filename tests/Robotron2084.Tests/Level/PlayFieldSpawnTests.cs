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
        var field = CreateField(4, 2, 2, 1, 6);

        Assert.Equal(6, field.Entities.Electrodes.GetLiveCount());
        Assert.Equal(4, field.Entities.Grunts.GetLiveCount());
        Assert.Equal(2, field.Entities.Hulks.GetLiveCount());
        Assert.Equal(2, field.Entities.Spheroids.GetLiveCount());
        Assert.Equal(1, field.Entities.Quarks.GetLiveCount());
        Assert.Equal(0, field.Entities.Enforcers.GetLiveCount()); // only dropped by spheroids later
        Assert.Equal(0, field.Entities.Tanks.GetLiveCount()); // only dropped by quarks later
    }

    [Fact]
    public void Constructor_NoTwoElectrodesOverlap()
    {
        var field = CreateField(4, 2, 2, 1, 6);
        IReadOnlyList<Electrode> electrodes = field.Entities.Electrodes;

        for (var i = 0; i < electrodes.Count; i++)
        for (var j = i + 1; j < electrodes.Count; j++)
            Assert.False(electrodes[i].GetBounds().Intersects(electrodes[j].GetBounds()),
                $"electrodes {i} and {j} overlap");
    }

    [Fact]
    public void Constructor_NoInitialSpawnOverlapsTheWall()
    {
        var field = CreateField(4, 2, 2, 1, 6);

        AssertEveryEntityIsFullyInsidePlayArea(e => e.GetBounds(), field.Entities.Electrodes);
    }

    [Fact]
    public void Constructor_InitialSpawnsRespectMinimumDistancesFromPlayer()
    {
        var field = CreateField(4, 2, 2, 1, 6);
        var playerStart = field.Player.Position;

        AssertAllAreFartherThan(field.Entities.Electrodes, playerStart, SpawnTuning.ElectrodeMinDistanceFromPlayer);
        AssertAllAreFartherThan(field.Entities.Grunts, playerStart, SpawnTuning.GruntMinDistanceFromPlayer);
        AssertAllAreFartherThan(field.Entities.Hulks, playerStart, SpawnTuning.HulkMinDistanceFromPlayer);
        AssertAllAreFartherThan(field.Entities.Spheroids, playerStart, SpheroidTuning.MinDistanceFromPlayer);
        // Quarks are EXCLUDED: the ROM ($4B48-4B5A) spawns them on the top or
        // bottom wall with a uniform X — no minimum-distance rule (notes 28).
    }

    [Fact]
    public void Constructor_QuarksSpawnOnTheTopOrBottomWall()
    {
        var field = CreateField(0, 0, 0, 3, 0);
        var bounds = field.Wall.PlayfieldBounds;

        Assert.Equal(3, field.Entities.Quarks.GetLiveCount());
        foreach (var quark in field.Entities.Quarks)
        {
            var bottomEdgeY = bounds.Bottom - quark.GetBounds().Height;
            Assert.True(quark.GetBounds().Y == bounds.Y || quark.GetBounds().Y == bottomEdgeY,
                $"quark at y {quark.GetBounds().Y} is not on a wall edge (top {bounds.Y}, bottom {bottomEdgeY})");
            Assert.InRange(quark.GetBounds().X, bounds.X, bounds.Right - quark.GetBounds().Width);
        }
    }

    private static PlayField CreateField(int grunts, int hulks, int spheroids, int quarks, int electrodes)
    {
        var parameters = new LevelParameters(
            1,
            grunts,
            HulkCount: hulks,
            SpheroidCount: spheroids,
            QuarkCount: quarks,
            ElectrodeCount: electrodes,
            MaxEnforcersPerSpheroid: 2,
            MaxTanksPerQuark: 2,
            EnemySpeedBonus: 0);

        return new PlayFieldBuilder().WithParameters(parameters).WithBounds(InnerBounds).WithSeed(1234).Build();
    }

    private static void AssertAllAreFartherThan<T>(IReadOnlyList<T> entities, IntVector2 playerStart,
        int minArcadePixels)
        where T : IEntity
    {
        var minSquared = ScreenSize.ToPortPixelsFromArcadePixels(minArcadePixels) *
                         (long)ScreenSize.ToPortPixelsFromArcadePixels(minArcadePixels);
        foreach (var entity in entities)
            Assert.True(IntVector2.ComputeDistanceSquared(entity.Position, playerStart) > minSquared,
                $"{typeof(T).Name} spawned too close to the player start");
    }

    private static void AssertEveryEntityIsFullyInsidePlayArea<T>(Func<T, Rectangle> bounds, IReadOnlyList<T> entities)
        where T : IEntity
    {
        // Every entity must be contained in the play area (i.e. not overlap the wall ring).
        foreach (var entity in entities)
        {
            var b = bounds(entity);
            Assert.True(
                InnerBounds.X <= b.X && b.Right <= InnerBounds.Right && InnerBounds.Y <= b.Y &&
                b.Bottom <= InnerBounds.Bottom,
                $"{typeof(T).Name} overlaps the wall: {b}");
        }
    }
}
