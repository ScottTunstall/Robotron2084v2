using Microsoft.Xna.Framework;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The robot registry's guard tests (notes §119). Adding a kind is meant to be one entity class plus one
/// <see cref="RobotKinds"/> row, so every table in the playfield that a new kind must appear in is checked here
/// against <see cref="RobotKind"/> — and the facts a kind declares (its wave count, its score, whether touching
/// it is fatal) are checked against a hand-written table, so a new kind cannot be added without a deliberate
/// decision about each of them.
/// </summary>
public sealed class PlayFieldRobotRegistryTests
{
    /// <summary>The lists the update and draw passes hold that no robot kind owns: the family, its two markers,
    /// the explosions and the spheroid/quark bursts.</summary>
    private const int NonRobotLists = 5;

    /// <summary>The kinds the wave table brings, and the count each row must read.
    /// Every other kind is made by another robot, so it has no wave count.</summary>
    private static readonly (RobotKind Kind, Func<LevelParameters, int> Count)[] WaveBrought =
    [
        (RobotKind.Electrode, static parameters => parameters.ElectrodeCount),
        (RobotKind.Grunt, static parameters => parameters.GruntCount),
        (RobotKind.Hulk, static parameters => parameters.HulkCount),
        (RobotKind.Spheroid, static parameters => parameters.SpheroidCount),
        (RobotKind.Quark, static parameters => parameters.QuarkCount),
        (RobotKind.Brain, static parameters => parameters.BrainCount),
    ];

    /// <summary>What one laser kill of each kind is worth (notes §11.3). A hulk is indestructible and an
    /// electrode scores nothing, so both are worth 0.</summary>
    private static readonly (RobotKind Kind, int Score)[] ExpectedScores =
    [
        (RobotKind.Electrode, ScoreValues.Electrode),
        (RobotKind.Grunt, ScoreValues.Grunt),
        (RobotKind.Hulk, 0),
        (RobotKind.Spheroid, ScoreValues.Spheroid),
        (RobotKind.Enforcer, ScoreValues.Enforcer),
        (RobotKind.Quark, ScoreValues.Quark),
        (RobotKind.Tank, ScoreValues.Tank),
        (RobotKind.Brain, ScoreValues.Brain),
        (RobotKind.Prog, ScoreValues.Prog),
        (RobotKind.Spark, ScoreValues.Spark),
        (RobotKind.TankShell, ScoreValues.TankShell),
        (RobotKind.CruiseMissile, ScoreValues.CruiseMissile),
    ];

    /// <summary>The kinds a touch from the player kills them. The electrode is NOT one of them: it has its own
    /// phase (<c>ResolvePlayerVsElectrodeCollision</c>), which also destroys the post.</summary>
    private static readonly RobotKind[] FatalOnContact =
    [
        RobotKind.Grunt,
        RobotKind.Hulk,
        RobotKind.Brain,
        RobotKind.Prog,
        RobotKind.Spark,
        RobotKind.TankShell,
        RobotKind.CruiseMissile,
    ];

    /// <summary>The entity class each kind's list holds — the hand-written half of the registry, so wiring a
    /// kind to the wrong list cannot pass unnoticed.</summary>
    private static readonly (RobotKind Kind, Type EntityType)[] ExpectedEntityTypes =
    [
        (RobotKind.Electrode, typeof(Electrode)),
        (RobotKind.Grunt, typeof(Grunt)),
        (RobotKind.Hulk, typeof(Hulk)),
        (RobotKind.Spheroid, typeof(Spheroid)),
        (RobotKind.Enforcer, typeof(Enforcer)),
        (RobotKind.Quark, typeof(Quark)),
        (RobotKind.Tank, typeof(Tank)),
        (RobotKind.Brain, typeof(Brain)),
        (RobotKind.Prog, typeof(Prog)),
        (RobotKind.Spark, typeof(Spark)),
        (RobotKind.TankShell, typeof(TankShell)),
        (RobotKind.CruiseMissile, typeof(CruiseMissile)),
    ];

    private static LevelParameters OneOfEverything => new(
        LevelNumber: 1,
        GruntCount: 2,
        ElectrodeCount: 3,
        HulkCount: 5,
        BrainCount: 7,
        SpheroidCount: 11,
        QuarkCount: 13);

    private static PlayField CreateField() =>
        new(TestSprites.Shared, 
            new LevelParameters(LevelNumber: 1),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(99),
            startingLives: 3);

    [Fact]
    public void EveryKindHasExactlyOneRow_InTheEnumsOrder()
    {
        RobotKind[] kinds = Enum.GetValues<RobotKind>();

        Assert.Equal(kinds.Length, RobotKinds.All.Length);
        for (int index = 0; index < kinds.Length; index++)
        {
            // The order of `All` is behaviour: it is the order the laser phases resolve in.
            Assert.Equal(kinds[index], RobotKinds.All[index].Kind);
            Assert.Equal(kinds[index], RobotKinds.Of(kinds[index]).Kind);
        }
    }

    [Fact]
    public void EveryKindsListIsWalkedOnceAndDrawnOnce()
    {
        PlayField field = CreateField();
        IEntityList[] robotLists = [.. Enum.GetValues<RobotKind>().Select(field.ListOf)];

        // One list per kind: no two kinds share a list, and none is missing.
        Assert.Equal(Enum.GetValues<RobotKind>().Length, robotLists.Distinct().Count());

        // The update pass holds every kind's list, plus the lists no kind owns, and no list twice.
        Assert.Equal(robotLists.Length + NonRobotLists, field.UpdateOrder.Count);
        Assert.Equal(field.UpdateOrder.Count, field.UpdateOrder.Distinct().Count());
        foreach (IEntityList list in robotLists)
        {
            Assert.Contains(list, field.UpdateOrder);
        }

        // The two draw passes hold every walked list exactly once between them, so nothing is invisible.
        IEntityList[] drawn = [.. field.DrawOrderBehindShots, .. field.DrawOrderInFrontOfShots];
        Assert.Equal(field.UpdateOrder.Count, drawn.Length);
        Assert.Equal(field.UpdateOrder.Count, drawn.Distinct().Count());
        foreach (IEntityList list in field.UpdateOrder)
        {
            Assert.Contains(list, drawn);
        }
    }

    [Fact]
    public void OnlyTheWaveBroughtKindsHaveAWaveCount_AndEachReadsItsOwnCount()
    {
        LevelParameters parameters = OneOfEverything;

        foreach ((RobotKind kind, Func<LevelParameters, int> count) in WaveBrought)
        {
            Func<LevelParameters, int>? row = RobotKinds.Of(kind).WaveCount;

            Assert.NotNull(row);
            Assert.Equal(count(parameters), row(parameters));
        }

        foreach (RobotKind kind in Enum.GetValues<RobotKind>())
        {
            bool waveBrought = WaveBrought.Any(entry => entry.Kind == kind);

            Assert.Equal(waveBrought, RobotKinds.Of(kind).WaveCount is not null);
            // A kind the wave brings builds the wave's own; a child build is its parent's business.
            Assert.Equal(waveBrought, RobotKinds.Of(kind).Spawn is not null);
        }
    }

    [Fact]
    public void EveryRowsScoreIsTheKindsScoreValue()
    {
        Assert.Equal(Enum.GetValues<RobotKind>().Length, ExpectedScores.Length);

        foreach ((RobotKind kind, int score) in ExpectedScores)
        {
            Assert.Equal(score, RobotKinds.Of(kind).Score);
        }
    }

    [Fact]
    public void OnlyTheKindsThatBumpIntoThePlayerAreFatalToTouch()
    {
        foreach (RobotKind kind in Enum.GetValues<RobotKind>())
        {
            Assert.Equal(FatalOnContact.Contains(kind), RobotKinds.Of(kind).KillsPlayerOnContact);
        }
    }

    [Fact]
    public void AKindsListRefusesToGuessWhenTheKindHasNoList()
    {
        PlayField field = CreateField();

        Assert.Throws<ArgumentOutOfRangeException>(() => field.ListOf((RobotKind)999));
    }

    [Fact]
    public void EveryKindsListHoldsThatKindsEntityType()
    {
        PlayField field = CreateField();

        Assert.Equal(Enum.GetValues<RobotKind>().Length, ExpectedEntityTypes.Length);
        foreach ((RobotKind kind, Type entityType) in ExpectedEntityTypes)
        {
            Type listType = field.ListOf(kind).GetType();
            Assert.Equal(entityType, Assert.Single(listType.GetGenericArguments()));
        }
    }
}