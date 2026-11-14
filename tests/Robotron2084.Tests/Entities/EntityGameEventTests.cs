using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Events;
using Robotron2084.Level;
using Robotron2084.Tests.Events;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>What the entities announce about themselves, and that the field connects them to where announcements go.</summary>
public sealed class EntityGameEventTests
{
    private static readonly IntVector2 Spot = new(120, 90);

    private static FamilyMember CreateFamilyMember(RecordingGameEvents recording)
    {
        return new FamilyMember(TestTextures.SharedTextureSet, Spot, FamilyMemberKind.Mommy, new Random(1)) { GameEvents = recording };
    }

    [Fact]
    public void AnEntityAnnouncesToNobodyUntilItIsGivenAPlaceToAnnounceTo()
    {
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, Spot, FamilyMemberKind.Mommy, new Random(1));

        familyMember.Kill();

        Assert.Same(NullGameEvents.Instance, familyMember.GameEvents);
    }

    [Fact]
    public void ARescuedFamilyMemberAnnouncesTheRescueOnce()
    {
        var recording = new RecordingGameEvents();
        var familyMember = CreateFamilyMember(recording);

        familyMember.Rescue();
        familyMember.Rescue();

        Assert.Equal([new GameEvent(GameEventKind.FamilyMemberRescued, Spot)], recording.Events);
    }

    [Fact]
    public void AKilledFamilyMemberAnnouncesTheDeathOnce()
    {
        var recording = new RecordingGameEvents();
        var familyMember = CreateFamilyMember(recording);

        familyMember.Kill();
        familyMember.Kill();

        Assert.Equal([new GameEvent(GameEventKind.FamilyMemberKilled, Spot)], recording.Events);
    }

    [Fact]
    public void AFamilyMemberAnnouncesThatReprogrammingBegan()
    {
        var recording = new RecordingGameEvents();
        var familyMember = CreateFamilyMember(recording);

        familyMember.BeginReprogramming();

        Assert.Equal([new GameEvent(GameEventKind.FamilyMemberReprogrammingBegan, Spot)], recording.Events);
    }

    [Fact]
    public void ADeadFamilyMemberCannotBeRescued()
    {
        var recording = new RecordingGameEvents();
        var familyMember = CreateFamilyMember(recording);
        familyMember.Kill();

        familyMember.Rescue();

        Assert.Single(recording.Events);
    }

    [Fact]
    public void AKilledPlayerAnnouncesTheDeath()
    {
        var recording = new RecordingGameEvents();
        var player = new Player(TestTextures.SharedTextureSet, Spot, 3, new Random(1)) { InvincibleForTesting = false, GameEvents = recording };

        player.Kill();
        player.Kill();

        Assert.Equal([new GameEvent(GameEventKind.PlayerKilled, Spot)], recording.Events);
    }

    [Fact]
    public void AnInvinciblePlayerAnnouncesNothing()
    {
        var recording = new RecordingGameEvents();
        var player = new Player(TestTextures.SharedTextureSet, Spot, 3, new Random(1)) { InvincibleForTesting = true, GameEvents = recording };

        player.Kill();

        Assert.Empty(recording.Events);
    }

    [Fact]
    public void AnExtraLifeIsAnnounced()
    {
        var recording = new RecordingGameEvents();
        var player = new Player(TestTextures.SharedTextureSet, Spot, 3, new Random(1)) { GameEvents = recording };

        player.AddLife();

        Assert.Equal([new GameEvent(GameEventKind.ExtraLifeAwarded, Spot)], recording.Events);
    }

    [Fact]
    public void AKilledGruntAnnouncesWhichRobotDiedOnce()
    {
        var recording = new RecordingGameEvents();
        var grunt = new Grunt(TestTextures.SharedTextureSet, Spot, new Random(1), 10) { GameEvents = recording };

        grunt.Kill();
        grunt.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Grunt)], recording.Events);
    }

    [Fact]
    public void AKilledProgAnnouncesItWasAProg()
    {
        var recording = new RecordingGameEvents();
        var prog = new Prog(TestTextures.SharedTextureSet, Spot, FamilyMemberKind.Daddy, new Random(1)) { GameEvents = recording };

        prog.Kill();
        prog.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Prog)], recording.Events);
    }

    [Fact]
    public void TheFieldConnectsEntitiesWhenTheyAreAdded()
    {
        var recording = new RecordingGameEvents();
        var fieldEntities = new FieldEntities(recording);
        var grunt = new Grunt(TestTextures.SharedTextureSet, Spot, new Random(1), 10);
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, Spot, FamilyMemberKind.Mikey, new Random(2));
        fieldEntities.Add(grunt);
        fieldEntities.Add(familyMember);

        grunt.Kill();
        familyMember.Rescue();

        Assert.Equal(
            [new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Grunt), new GameEvent(GameEventKind.FamilyMemberRescued, Spot)],
            recording.Events);
    }
}
