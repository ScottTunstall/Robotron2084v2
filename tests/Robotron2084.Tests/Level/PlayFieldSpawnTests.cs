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
        var playField = CreateField(4, 2, 2, 1, 6);

        Assert.Equal(6, playField.FieldEntities.Electrodes.Count(entity => !entity.IsDead()));
        Assert.Equal(4, playField.FieldEntities.Grunts.Count(entity => !entity.IsDead()));
        Assert.Equal(2, playField.FieldEntities.Hulks.Count(entity => !entity.IsDead()));
        Assert.Equal(2, playField.FieldEntities.Spheroids.Count(entity => !entity.IsDead()));
        Assert.Equal(1, playField.FieldEntities.Quarks.Count(entity => !entity.IsDead()));
        Assert.Equal(0, playField.FieldEntities.Enforcers.Count(entity => !entity.IsDead())); // only dropped by spheroids later
        Assert.Equal(0, playField.FieldEntities.Tanks.Count(entity => !entity.IsDead())); // only dropped by quarks later
    }

    [Fact]
    public void Constructor_NoTwoElectrodesOverlap()
    {
        var playField = CreateField(4, 2, 2, 1, 6);
        IReadOnlyList<Electrode> electrodes = playField.FieldEntities.Electrodes;

        for (var i = 0; i < electrodes.Count; i++)
        for (var j = i + 1; j < electrodes.Count; j++)
            Assert.False(electrodes[i].GetBounds().Intersects(electrodes[j].GetBounds()),
                $"electrodes {i} and {j} overlap");
    }

    [Fact]
    public void Constructor_NoInitialSpawnOverlapsTheWall()
    {
        var playField = CreateField(4, 2, 2, 1, 6);

        AssertEveryEntityIsFullyInsidePlayArea(e => e.GetBounds(), playField.FieldEntities.Electrodes);
    }

    [Fact]
    public void Constructor_InitialSpawnsRespectMinimumDistancesFromPlayer()
    {
        var playField = CreateField(4, 2, 2, 1, 6);
        var playerStart = playField.Player.Position;

        AssertAllAreFartherThan(playField.FieldEntities.Electrodes, playerStart, SpawnTuning.ElectrodeMinDistanceFromPlayer);
        AssertAllAreFartherThan(playField.FieldEntities.Grunts, playerStart, SpawnTuning.GruntMinDistanceFromPlayer);
        AssertAllAreFartherThan(playField.FieldEntities.Hulks, playerStart, SpawnTuning.HulkMinDistanceFromPlayer);
        AssertAllAreFartherThan(playField.FieldEntities.Spheroids, playerStart, SpheroidTuning.MinDistanceFromPlayer);
        // Quarks are EXCLUDED: the ROM ($4B48-4B5A) spawns them on the top or
        // bottom wall with a uniform X — no minimum-distance rule (notes 28).
    }

    [Fact]
    public void Constructor_QuarksSpawnOnTheTopOrBottomWall()
    {
        var playField = CreateField(0, 0, 0, 3, 0);
        var bounds = playField.PlayfieldWall.PlayfieldBounds;

        Assert.Equal(3, playField.FieldEntities.Quarks.Count(entity => !entity.IsDead()));
        foreach (var quark in playField.FieldEntities.Quarks)
        {
            var bottomEdgeY = bounds.Bottom - quark.GetBounds().Height;
            Assert.True(quark.GetBounds().Y == bounds.Y || quark.GetBounds().Y == bottomEdgeY,
                $"quark at y {quark.GetBounds().Y} is not on a wall edge (top {bounds.Y}, bottom {bottomEdgeY})");
            Assert.InRange(quark.GetBounds().X, bounds.X, bounds.Right - quark.GetBounds().Width);
        }
    }

    private static PlayField CreateField(int grunts, int hulks, int spheroids, int quarks, int electrodes)
    {
        var waveParameters = new WaveParameters(
            1,
            grunts,
            HulkCount: hulks,
            SpheroidCount: spheroids,
            QuarkCount: quarks,
            ElectrodeCount: electrodes,
            MaxEnforcersPerSpheroid: 2,
            MaxTanksPerQuark: 2,
            EnemySpeedBonus: 0);

        return new PlayFieldBuilder().WithParameters(waveParameters).WithBounds(InnerBounds).WithSeed(1234).Build();
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
