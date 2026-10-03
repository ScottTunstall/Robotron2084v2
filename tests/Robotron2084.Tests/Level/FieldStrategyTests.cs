using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Robotron2084.Level.Spawning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The classes the playfield hands its jobs to: the wave spawners, the collision rules and their order, the contact
/// test, the family list and the lists of entities.
/// </summary>
public sealed class FieldStrategyTests
{
    [Fact]
    public void EveryKindTheWaveBrings_HasItsOwnSpawner_AndEveryOtherKindHasNone()
    {
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            Assert.Equal(robot.WaveCount is not null, robot.Spawn is not null);
        }

        Type[] spawners = RobotKinds.All.Where(robot => robot.Spawn is not null).Select(robot => robot.Spawn!.GetType()).ToArray();
        Assert.Equal(spawners.Length, spawners.Distinct().Count());
    }

    [Fact]
    public void TheCollisionRulesRunInTheArcadesOrder()
    {
        Type[] order = CollisionPhases.InArcadeOrder.Select(phase => phase.GetType()).ToArray();

        Assert.Equal(
            [
                typeof(LaserCollisionPhase),
                typeof(RobotVsElectrodeCollisionPhase),
                typeof(PlayerVsElectrodeCollisionPhase),
                typeof(PlayerContactKillPhase),
                typeof(BrainVictimReleasePhase),
                typeof(BrainCatchPhase),
                typeof(HulkVsHumanCollisionPhase),
                typeof(PlayerRescuePhase),
            ],
            order);
    }

    [Fact]
    public void TheBoxContactTest_SaysTwoThingsTouch_OnlyWhenTheirBoxesOverlap()
    {
        var test = new BoxContactTest();
        var first = new Electrode(TestSprites.Shared, new IntVector2(100, 100));
        var overlapping = new Electrode(TestSprites.Shared, new IntVector2(100 + (first.Bounds.Width / 2), 100));
        var apart = new Electrode(TestSprites.Shared, new IntVector2(100 + first.Bounds.Width, 100));

        Assert.True(test.Touches(first, overlapping));
        Assert.False(test.Touches(first, apart));
    }

    [Fact]
    public void TheFamilyList_HandsOutPlacesInOrder_AndFindsTheNearestFreeMember()
    {
        var family = new FamilyList();
        var mikey = new Human(TestSprites.Shared, new IntVector2(100, 100), HumanKind.Mikey, new Random(1));
        var mommy = new Human(TestSprites.Shared, new IntVector2(300, 100), HumanKind.Mommy, new Random(2));
        Assert.Equal(FamilyList.FirstSlot, family.GetNearestSlot(new IntVector2(0, 0)));
        Assert.Null(family.GetLastMemberPosition());

        family.Add(mikey);
        family.Add(mommy);

        Assert.Equal(FamilyList.FirstSlot, mikey.FamilySlot);
        Assert.Equal(FamilyList.FirstSlot + 1, mommy.FamilySlot);
        Assert.Equal(mommy.FamilySlot, family.GetNearestSlot(new IntVector2(290, 100)));
        Assert.Equal(mommy, family.GetMemberInSlot(mommy.FamilySlot));
        Assert.Equal(mommy.Position, family.GetLastMemberPosition());

        // In a brain's hold she is off the list: her place reads empty, and the nearest is whoever is left.
        mommy.BeginReprogramming();
        Assert.Null(family.GetMemberInSlot(mommy.FamilySlot));
        Assert.Equal(mikey.FamilySlot, family.GetNearestSlot(new IntVector2(290, 100)));
        Assert.True(family.AnyAvailable());

        mikey.Kill();
        Assert.False(family.AnyAvailable());
    }

    [Fact]
    public void TheFamilyList_GivesATieToTheLaterPlace()
    {
        var family = new FamilyList();
        family.Add(new Human(TestSprites.Shared, new IntVector2(100, 100), HumanKind.Mikey, new Random(1)));
        family.Add(new Human(TestSprites.Shared, new IntVector2(100, 100), HumanKind.Daddy, new Random(2)));

        Assert.Equal(FamilyList.FirstSlot + 1, family.GetNearestSlot(new IntVector2(100, 100)));
    }

    [Fact]
    public void TheFieldsEntities_HaveAListForEveryKind_AndIgnoreHulksAndElectrodesWhenTheWaveIsWon()
    {
        var entities = new FieldEntities();
        foreach (RobotKind kind in Enum.GetValues<RobotKind>())
        {
            Assert.NotNull(entities.GetList(kind));
        }

        entities.Electrodes.Add(new Electrode(TestSprites.Shared, new IntVector2(10, 10)));
        entities.Hulks.Add(new Hulk(TestSprites.Shared, new IntVector2(50, 50), new Random(1), beatIntervalRomFrames: 8, () => IntVector2.Zero));
        Assert.True(entities.AreEnemiesGone());

        entities.Grunts.Add(new Grunt(TestSprites.Shared, new IntVector2(90, 90)));
        Assert.False(entities.AreEnemiesGone());
    }
}
