using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     The classes the playfield hands its jobs to: the wave spawners, the collision rules and their order, the contact
///     test, the family list and the lists of entities.
/// </summary>
public sealed class FieldStrategyTests
{
    [Fact]
    public void EveryKindTheWaveBrings_HasItsOwnSpawner_AndEveryOtherKindHasNone()
    {
        foreach (var robot in RobotKinds.All) Assert.Equal(robot.WaveCount is not null, robot.Spawn is not null);

        var spawners = RobotKinds.All.Where(robot => robot.Spawn is not null).Select(robot => robot.Spawn!.GetType())
            .ToArray();
        Assert.Equal(spawners.Length, spawners.Distinct().Count());
    }

    [Fact]
    public void TheCollisionRulesRunInTheArcadesOrder()
    {
        var order = CollisionRules.InArcadeOrder.Select(phase => phase.GetType()).ToArray();

        Assert.Equal(
            [
                typeof(LaserCollisionRule),
                typeof(RobotVsElectrodeCollisionRule),
                typeof(PlayerVsElectrodeCollisionRule),
                typeof(PlayerContactKillRule),
                typeof(BrainVictimReleaseRule),
                typeof(BrainCatchRule),
                typeof(HulkVsFamilyMemberCollisionRule),
                typeof(PlayerRescueRule)
            ],
            order);
    }

    [Fact]
    public void TheBoxContactTest_SaysTwoThingsTouch_OnlyWhenTheirBoxesOverlap()
    {
        var boxContactTest = new BoxContactTest();
        var firstElectrode = new Electrode(TestTextures.SharedTextureSet, new IntVector2(100, 100));
        var overlappingElectrode = new Electrode(TestTextures.SharedTextureSet, new IntVector2(100 + firstElectrode.GetBounds().Width / 2, 100));
        var apartElectrode = new Electrode(TestTextures.SharedTextureSet, new IntVector2(100 + firstElectrode.GetBounds().Width, 100));

        Assert.True(boxContactTest.Touches(firstElectrode, overlappingElectrode));
        Assert.False(boxContactTest.Touches(firstElectrode, apartElectrode));
    }

    [Fact]
    public void TheFamilyList_HandsOutPlacesInOrder_AndFindsTheNearestFreeMember()
    {
        var familyList = new FamilyList();
        var mikeyFamilyMember = new FamilyMember(TestTextures.SharedTextureSet, new IntVector2(100, 100), FamilyMemberKind.Mikey, new Random(1));
        var mommyFamilyMember = new FamilyMember(TestTextures.SharedTextureSet, new IntVector2(300, 100), FamilyMemberKind.Mommy, new Random(2));
        Assert.Equal(FamilyList.FirstSlot, familyList.GetNearestSlot(new IntVector2(0, 0)));
        Assert.Empty(familyList.GetOccupiedSlots());

        familyList.Add(mikeyFamilyMember);
        familyList.Add(mommyFamilyMember);

        Assert.Equal(FamilyList.FirstSlot, mikeyFamilyMember.FamilySlot);
        Assert.Equal(FamilyList.FirstSlot + 1, mommyFamilyMember.FamilySlot);
        Assert.Equal(mommyFamilyMember.FamilySlot, familyList.GetNearestSlot(new IntVector2(290, 100)));
        Assert.Equal(mommyFamilyMember, familyList.GetMemberInSlot(mommyFamilyMember.FamilySlot));
        Assert.Equal([mikeyFamilyMember.FamilySlot, mommyFamilyMember.FamilySlot], familyList.GetOccupiedSlots());

        // In a brain's hold she is off the list: her place reads empty, and the nearest is whoever is left.
        mommyFamilyMember.BeginReprogramming();
        Assert.Null(familyList.GetMemberInSlot(mommyFamilyMember.FamilySlot));
        Assert.Equal(mikeyFamilyMember.FamilySlot, familyList.GetNearestSlot(new IntVector2(290, 100)));
        Assert.True(familyList.AnyAvailable());

        mikeyFamilyMember.Kill();
        Assert.False(familyList.AnyAvailable());
    }

    [Fact]
    public void TheFamilyList_GivesATieToTheLaterPlace()
    {
        var familyList = new FamilyList();
        familyList.Add(new FamilyMember(TestTextures.SharedTextureSet, new IntVector2(100, 100), FamilyMemberKind.Mikey, new Random(1)));
        familyList.Add(new FamilyMember(TestTextures.SharedTextureSet, new IntVector2(100, 100), FamilyMemberKind.Daddy, new Random(2)));

        Assert.Equal(FamilyList.FirstSlot + 1, familyList.GetNearestSlot(new IntVector2(100, 100)));
    }

    [Fact]
    public void TheFieldsEntities_HaveAListForEveryKind_AndIgnoreHulksAndElectrodesWhenTheWaveIsWon()
    {
        var fieldEntities = new FieldEntities();
        foreach (var kind in Enum.GetValues<RobotKind>()) Assert.NotNull(fieldEntities.GetList(kind));

        fieldEntities.Electrodes.Add(new Electrode(TestTextures.SharedTextureSet, new IntVector2(10, 10)));
        fieldEntities.Hulks.Add(
            new Hulk(TestTextures.SharedTextureSet, new IntVector2(50, 50), new Random(1), 8, () => IntVector2.Zero));
        Assert.True(fieldEntities.AreEnemiesGone());

        fieldEntities.Grunts.Add(new Grunt(TestTextures.SharedTextureSet, new IntVector2(90, 90)));
        Assert.False(fieldEntities.AreEnemiesGone());
    }

    [Fact]
    public void TheField_AnswersWithoutLettingCallersReachThroughIt()
    {
        var playField = new PlayFieldBuilder().Build();
        var grunt = new Grunt(TestTextures.SharedTextureSet, new IntVector2(90, 90));
        playField.FieldEntities.Add(grunt);

        Assert.Contains(grunt, playField.FieldEntities.GetEntities(RobotKind.Grunt));
        Assert.Equal(playField.Player.Position, playField.GetPlayerPosition());
        Assert.Equal(playField.PlayfieldWall.PlayfieldBounds, playField.GetPlayfieldBounds());
        Assert.True(playField.HitsWall(new Rectangle(playField.PlayfieldWall.GetOuterBounds().X, playField.PlayfieldWall.GetOuterBounds().Y, 4, 4)));
        Assert.False(playField.HitsWall(new Rectangle(playField.GetPlayfieldBounds().X + 40, playField.GetPlayfieldBounds().Y + 40,
            4, 4)));
        Assert.True(playField.IsPlayerAlive());
        Assert.False(playField.IsPlayerDead());
    }

    [Fact]
    public void ACollisionRule_OnlyReports_AndTheFieldResponds()
    {
        var playField = new PlayFieldBuilder().Build();
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, playField.Player.Position, FamilyMemberKind.Mommy, new Random(1));
        playField.FieldEntities.Add(familyMember);

        var results = new PlayerRescueRule().Detect(playField, playField.FieldEntities).ToArray();

        // The rule found the rescue and changed nothing.
        var rescued = Assert.IsType<PlayerRescuedFamilyMemberResult>(Assert.Single(results));
        Assert.Same(familyMember, rescued.FamilyMember);
        Assert.True(familyMember.IsGraspable());
        Assert.Equal(0, playField.RescuesThisLife);

        // The field's response is what rescues them.
        new CollisionResponder(playField).Respond(rescued);
        Assert.False(familyMember.IsGraspable());
        Assert.Equal(1, playField.RescuesThisLife);
    }

    [Fact]
    public void TheLaserRule_ReportsOneHitPerLaser_AsTheFieldKillsTheLaserBetweenReports()
    {
        var playField = new PlayFieldBuilder().Build();
        var bounds = playField.GetPlayfieldBounds();
        IntVector2 spot = new(bounds.X + 200, bounds.Y + 200);
        playField.FieldEntities.Add(new Grunt(TestTextures.SharedTextureSet, spot));
        playField.FieldEntities.Add(new Grunt(TestTextures.SharedTextureSet, spot));
        playField.PlayerLaserSlots.Fire(spot, Direction8.Up);
        var collisionResponder = new CollisionResponder(playField);

        var hits = 0;
        foreach (var result in new LaserCollisionRule().Detect(playField, playField.FieldEntities))
        {
            hits++;
            collisionResponder.Respond(result);
        }

        // Two grunts stand under one laser, and the laser is spent on the first.
        Assert.Equal(1, hits);
        Assert.Equal(1, playField.FieldEntities.Grunts.Count(grunt => grunt.IsAlive()));
    }
}
