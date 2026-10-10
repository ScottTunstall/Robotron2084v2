using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>What a death keeps (ROM <c>PLEND</c>, <c>PLSAV</c>, <c>PLRES</c>, notes §134).</summary>
public sealed class WaveSurvivorsTests
{
    private static PlayField CreateField(LevelParameters parameters) =>
        new PlayFieldBuilder().WithParameters(parameters).WithSeed(7).Build();

    private static Enforcer CreateEnforcer() => new(TestSprites.Shared, new IntVector2(100, 100), new Random(1));

    [Fact]
    public void WhatWasKilledOrRescued_StaysGone_AndWhatIsLeftIsKept()
    {
        PlayField field = CreateField(new LevelParameters(
            LevelNumber: 5, GruntCount: 4, ElectrodeCount: 3, MommyCount: 2, DaddyCount: 1, MikeyCount: 2,
            HulkCount: 2, BrainCount: 1, SpheroidCount: 3, QuarkCount: 2));
        field.Entities.Grunts[0].Kill();
        field.Entities.Electrodes[0].Kill();
        field.Entities.Family.Members.First(human => human.Kind == HumanKind.Mommy).Kill();
        field.Entities.Family.Members.First(human => human.Kind == HumanKind.Mikey).Kill();
        field.Entities.Spheroids[0].Kill();

        LevelParameters left = WaveSurvivors.GetFrom(field);

        Assert.Equal(3, left.GruntCount);
        Assert.Equal(2, left.ElectrodeCount);
        Assert.Equal(1, left.MommyCount);
        Assert.Equal(1, left.DaddyCount);
        Assert.Equal(1, left.MikeyCount);
        Assert.Equal(2, left.HulkCount);
        Assert.Equal(1, left.BrainCount);
        Assert.Equal(2, left.SpheroidCount);
        Assert.Equal(2, left.QuarkCount);
        Assert.Equal(5, left.LevelNumber);
    }

    [Fact]
    public void ALifeThatHasBegunToEndIsAlreadyOffTheCount()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 2));
        field.Entities.Grunts[0].Kill(); // dying, not yet gone: the ROM has already counted it off

        Assert.True(field.Entities.Grunts[0].IsDying() || field.Entities.Grunts[0].IsDead());
        Assert.Equal(1, WaveSurvivors.GetFrom(field).GruntCount);
    }

    [Theory]
    [InlineData(0, 2, 2, 2)] // no enforcers: the spheroids are kept as they are
    [InlineData(3, 0, 3, 1)] // enforcers but no spheroids: at least one comes back
    [InlineData(3, 1, 3, 1)] // fewer than four make none, so only the spheroid left
    [InlineData(7, 1, 3, 2)] // one for every four
    [InlineData(13, 2, 3, 3)] // 3 + 2 = 5, but never more than the wave began with
    public void LeftoverEnforcers_TurnBackIntoSpheroids_AsTheRomDoes(int enforcers, int spheroidsLeft, int startedWith, int expected)
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, SpheroidCount: startedWith));
        for (int i = 0; i < startedWith - spheroidsLeft; i++)
        {
            field.Entities.Spheroids[i].Kill();
        }

        for (int i = 0; i < enforcers; i++)
        {
            field.Entities.Enforcers.Add(CreateEnforcer());
        }

        Assert.Equal(expected, WaveSurvivors.GetFrom(field).SpheroidCount);
    }

    [Fact]
    public void TheGrunts_KeepTheSpeedLimitTheyReached_ButNeverSlowerThanTheWavesFloorAllows()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 3, GruntMoveDelay: 20, GruntSpeedFloor: 9));
        foreach (Grunt grunt in field.Entities.Grunts)
        {
            grunt.SpeedUp(floorBeats: 1);
        }

        Assert.Equal(17, WaveSurvivors.GetFrom(field).GruntMoveDelay); // 20 x 7/8, truncated: kept
        Assert.Equal(9, WaveSurvivors.GetFrom(field).GruntSpeedFloor); // the floor goes back to the wave's own
    }

    [Fact]
    public void TheGruntLimit_IsRaisedToTheFloor_WhenTheWaveHasPushedItBelowIt()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 2, GruntMoveDelay: 20, GruntSpeedFloor: 15));
        foreach (Grunt grunt in field.Entities.Grunts)
        {
            grunt.WaveSpeedTick(floorBeats: 1);
            grunt.WaveSpeedTick(floorBeats: 1);
        }

        Assert.True(field.Entities.Grunts[0].MoveDelayBeats < 15);
        Assert.Equal(15, WaveSurvivors.GetFrom(field).GruntMoveDelay); // ROM PLEND2: ROBSPD raised to RMXSPD
    }

    [Fact]
    public void ATankThatWasAlive_ComesBackAtFullSize_AndOneThatWasKilledDoesNot()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, TankCount: 3));

        Assert.Equal(3, field.Entities.Tanks.Count);
        Assert.All(field.Entities.Tanks, tank => Assert.False(tank.IsBeingBorn()));

        field.Entities.Tanks[0].Kill();
        Assert.Equal(2, WaveSurvivors.GetFrom(field).TankCount);
    }

    [Fact]
    public void TheNextLifeIsMadeFromWhatWasLeft()
    {
        PlayField first = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: 5, MikeyCount: 2));
        first.Entities.Grunts[0].Kill();
        first.Entities.Grunts[1].Kill();

        PlayField next = CreateField(WaveSurvivors.GetFrom(first));

        Assert.Equal(3, next.Entities.Grunts.Count);
        Assert.Equal(2, next.Entities.Family.Members.Count);
    }
}
