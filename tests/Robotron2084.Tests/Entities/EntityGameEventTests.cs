using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;
using Robotron2084.Core;
using Robotron2084.Entities.Models;
using Robotron2084.Entities;
using Robotron2084.Events.Models;
using Robotron2084.Events;
using Robotron2084.Level.Models;
using Robotron2084.Level;
using Robotron2084.Tests.Events;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>What the entities announce about themselves, and that the field gives them the place to announce to.</summary>
public sealed class EntityGameEventTests
{
    private static readonly IntVector2 Spot = new(120, 90);

    private static FamilyMember CreateFamilyMember(RecordingGameEvents recording)
    {
        return new FamilyMember(TestEntityParams.Create(new Random(1), recording), Spot, FamilyMemberKind.Mommy);
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
        var player = new Player(TestEntityParams.Create(new Random(1), recording), Spot, new LivesCounter(3)) { InvincibleForTesting = false };

        player.Kill();
        player.Kill();

        Assert.Equal([new GameEvent(GameEventKind.PlayerKilled, Spot)], recording.Events);
    }

    [Fact]
    public void AnInvinciblePlayerAnnouncesNothing()
    {
        var recording = new RecordingGameEvents();
        var player = new Player(TestEntityParams.Create(new Random(1), recording), Spot, new LivesCounter(3)) { InvincibleForTesting = true };

        player.Kill();

        Assert.Empty(recording.Events);
    }

    [Fact]
    public void AnExtraLifeIsAnnounced()
    {
        var recording = new RecordingGameEvents();
        var player = new Player(TestEntityParams.Create(new Random(1), recording), Spot, new LivesCounter(3));

        player.AddLife();

        Assert.Equal([new GameEvent(GameEventKind.ExtraLifeAwarded, Spot)], recording.Events);
    }

    [Fact]
    public void AKilledGruntAnnouncesWhichRobotDiedOnce()
    {
        var recording = new RecordingGameEvents();
        var grunt = new Grunt(TestEntityParams.Create(new Random(1), recording), Spot, 10);

        grunt.Kill();
        grunt.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Grunt)], recording.Events);
    }

    [Fact]
    public void AKilledProgAnnouncesItWasAProg()
    {
        var recording = new RecordingGameEvents();
        var prog = new Prog(TestEntityParams.Create(new Random(1), recording), Spot, FamilyMemberKind.Daddy);

        prog.Kill();
        prog.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Prog)], recording.Events);
    }

    [Fact]
    public void AKilledElectrodeAnnouncesItOnce()
    {
        var recording = new RecordingGameEvents();
        var electrode = new Electrode(TestTextures.SharedTextureSet, recording, Spot);

        electrode.Kill();
        electrode.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Electrode)], recording.Events);
    }

    [Fact]
    public void AHulkThatIsKnockedBackAnnouncesIt()
    {
        var recording = new RecordingGameEvents();
        var hulk = new Hulk(TestEntityParams.Create(new Random(1), recording), Spot, 2, () => Spot);

        hulk.ApplyKnockback(Direction8.Right);

        Assert.Equal([new GameEvent(GameEventKind.HulkKnockedBack, hulk.Position)], recording.Events);
    }

    [Fact]
    public void AKilledGorfAnnouncesTheKillOnce()
    {
        var recording = new RecordingGameEvents();
        var bounds = new Rectangle(20, 30, 600, 400);
        var gorf = new Gorf(TestEntityParams.Create(new Random(3), recording), new GorfPlayfieldParams(bounds), new GorfDropParams(10));

        gorf.Kill();
        gorf.Kill();

        Assert.Equal([new GameEvent(GameEventKind.GorfKilled, gorf.Position)], recording.Events);
    }

    [Fact]
    public void AKilledSparkAnnouncesItWasASpark()
    {
        var recording = new RecordingGameEvents();
        var spark = new Spark(TestEntityParams.Create(new Random(1), recording), Spot, new IntVector2(300, 200));

        spark.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Spark)], recording.Events);
    }

    [Fact]
    public void AKilledTankShellAnnouncesItWasATankShell()
    {
        var recording = new RecordingGameEvents();
        var bounds = new Rectangle(20, 30, 600, 400);
        var tankShell = new TankShell(TestEntityParams.Create(new Random(1), recording), Spot, new TankShellAimParams(new IntVector2(300, 200), 3, bounds));

        tankShell.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, tankShell.Position, RobotKind.TankShell)], recording.Events);
    }

    [Fact]
    public void AKilledCruiseMissileAnnouncesItWasACruiseMissile()
    {
        var recording = new RecordingGameEvents();
        var cruiseMissile = new CruiseMissile(TestEntityParams.Create(new Random(1), recording), Spot, new IntVector2(300, 200));

        cruiseMissile.Kill();

        Assert.Equal([new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.CruiseMissile)], recording.Events);
    }

    [Fact]
    public void ALaserThatIsUsedUpAnnouncesItOnce()
    {
        var recording = new RecordingGameEvents();
        var laser = new PlayerLaser(TestTextures.SharedTextureSet, recording, Spot, Direction8.Up);

        laser.Kill();
        laser.Kill();

        Assert.Equal([new GameEvent(GameEventKind.PlayerLaserDestroyed, Spot)], recording.Events);
    }

    [Fact]
    public void TheFieldGivesItsGameEventsToThePlayerAndToTheEntitiesItBuilds()
    {
        var recording = new RecordingGameEvents();
        var playField = new PlayFieldBuilder().WithGameEvents(recording).WithPlayerInvincible(false).Build();
        playField.SpawnProg(Spot, FamilyMemberKind.Mikey);
        var prog = playField.FieldEntities.Progs.FieldEntities.OfType<Prog>().Single();

        prog.Kill();
        playField.KillPlayer();

        Assert.Equal(
            [new GameEvent(GameEventKind.RobotKilled, Spot, RobotKind.Prog), new GameEvent(GameEventKind.PlayerKilled, playField.Player.Position)],
            recording.Events);
    }
}
