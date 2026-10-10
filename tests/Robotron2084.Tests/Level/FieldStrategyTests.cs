using Microsoft.Xna.Framework;
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
        Type[] order = CollisionRules.InArcadeOrder.Select(phase => phase.GetType()).ToArray();

        Assert.Equal(
            [
                typeof(LaserCollisionRule),
                typeof(RobotVsElectrodeCollisionRule),
                typeof(PlayerVsElectrodeCollisionRule),
                typeof(PlayerContactKillRule),
                typeof(BrainVictimReleaseRule),
                typeof(BrainCatchRule),
                typeof(HulkVsHumanCollisionRule),
                typeof(PlayerRescueRule),
            ],
            order);
    }

    [Fact]
    public void TheBoxContactTest_SaysTwoThingsTouch_OnlyWhenTheirBoxesOverlap()
    {
        var test = new BoxContactTest();
        var first = new Electrode(TestSprites.Shared, new IntVector2(100, 100));
        var overlapping = new Electrode(TestSprites.Shared, new IntVector2(100 + (first.GetBounds().Width / 2), 100));
        var apart = new Electrode(TestSprites.Shared, new IntVector2(100 + first.GetBounds().Width, 100));

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

    [Fact]
    public void TheField_AnswersWithoutLettingCallersReachThroughIt()
    {
        PlayField field = new PlayFieldBuilder().Build();
        var grunt = new Grunt(TestSprites.Shared, new IntVector2(90, 90));
        field.Entities.Add(grunt);

        Assert.Contains(grunt, field.Entities.GetEntities(RobotKind.Grunt));
        Assert.Equal(field.Player.Position, field.GetPlayerPosition());
        Assert.Equal(field.Wall.PlayfieldBounds, field.GetPlayfieldBounds());
        Assert.True(field.HitsWall(new Microsoft.Xna.Framework.Rectangle(field.Wall.GetOuterBounds().X, field.Wall.GetOuterBounds().Y, 4, 4)));
        Assert.False(field.HitsWall(new Microsoft.Xna.Framework.Rectangle(field.GetPlayfieldBounds().X + 40, field.GetPlayfieldBounds().Y + 40, 4, 4)));
        Assert.True(field.IsPlayerAlive());
        Assert.False(field.IsPlayerDead());
    }

    [Fact]
    public void ACollisionRule_OnlyReports_AndTheFieldResponds()
    {
        PlayField field = new PlayFieldBuilder().Build();
        var human = new Human(TestSprites.Shared, field.Player.Position, HumanKind.Mommy, new Random(1));
        field.Entities.Add(human);

        CollisionResult[] results = new PlayerRescueRule().Detect(field, field.Entities).ToArray();

        // The rule found the rescue and changed nothing.
        PlayerRescuedHumanResult rescued = Assert.IsType<PlayerRescuedHumanResult>(Assert.Single(results));
        Assert.Same(human, rescued.Human);
        Assert.True(human.IsGraspable());
        Assert.Equal(0, field.RescuesThisLife);

        // The field's response is what rescues them.
        new CollisionResponder(field).Respond(rescued);
        Assert.False(human.IsGraspable());
        Assert.Equal(1, field.RescuesThisLife);
    }

    [Fact]
    public void TheLaserRule_ReportsOneHitPerLaser_AsTheFieldKillsTheLaserBetweenReports()
    {
        PlayField field = new PlayFieldBuilder().Build();
        Rectangle bounds = field.GetPlayfieldBounds();
        IntVector2 spot = new(bounds.X + 200, bounds.Y + 200);
        field.Entities.Add(new Grunt(TestSprites.Shared, spot));
        field.Entities.Add(new Grunt(TestSprites.Shared, spot));
        field.PlayerLasers.TryFire(spot, Direction8.Up, out _);
        var responder = new CollisionResponder(field);

        int hits = 0;
        foreach (CollisionResult result in new LaserCollisionRule().Detect(field, field.Entities))
        {
            hits++;
            responder.Respond(result);
        }

        // Two grunts stand under one laser, and the laser is spent on the first.
        Assert.Equal(1, hits);
        Assert.Equal(1, field.Entities.Grunts.Count(grunt => grunt.IsAlive()));
    }
}
