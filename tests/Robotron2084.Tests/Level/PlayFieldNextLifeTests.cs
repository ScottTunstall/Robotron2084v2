using Robotron2084.Core.Models;
using Robotron2084.Entities;
using Robotron2084.Entities.Extensions;
using Robotron2084.Entities.Models;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The wave a new life starts with: what a death keeps (ROM <c>PLEND</c>, <c>PLSAV</c>, <c>PLRES</c>, notes §134).</summary>
public sealed class PlayFieldNextLifeTests
{
    [Fact]
    public void ALifeThatHasBegunToEndIsAlreadyOffTheCount()
    {
        var playField = CreateField(new WaveParams(1, 2));
        playField.FieldEntities.Grunts[0].Kill(); // dying, not yet gone: the ROM has already counted it off

        Assert.True(playField.FieldEntities.Grunts[0].IsDying() || playField.FieldEntities.Grunts[0].IsDead());
        Assert.Equal(1, playField.GetWaveParamsForNextLife().GruntCount);
    }

    [Fact]
    public void ATankThatWasAlive_ComesBackAtFullSize_AndOneThatWasKilledDoesNot()
    {
        var playField = CreateField(new WaveParams(1, TankCount: 3));

        Assert.Equal(3, playField.FieldEntities.Tanks.Count);
        Assert.All(playField.FieldEntities.Tanks, tank => Assert.False(tank.IsGrowing()));

        playField.FieldEntities.Tanks[0].Kill();
        Assert.Equal(2, playField.GetWaveParamsForNextLife().TankCount);
    }

    [Theory]
    [InlineData(0, 2, 2, 2)] // no enforcers: the spheroids are kept as they are
    [InlineData(3, 0, 3, 1)] // enforcers but no spheroids: at least one comes back
    [InlineData(3, 1, 3, 1)] // fewer than four make none, so only the spheroid left
    [InlineData(7, 1, 3, 2)] // one for every four
    [InlineData(13, 2, 3, 3)] // 3 + 2 = 5, but never more than the wave began with
    public void LeftoverEnforcers_TurnBackIntoSpheroids_AsTheArcadeDoes(int enforcers, int spheroidsLeft, int startedWith,
        int expected)
    {
        var playField = CreateField(new WaveParams(1, SpheroidCount: startedWith));
        for (var i = 0; i < startedWith - spheroidsLeft; i++) playField.FieldEntities.Spheroids[i].Kill();

        for (var i = 0; i < enforcers; i++) playField.FieldEntities.Enforcers.Add(CreateEnforcer());

        Assert.Equal(expected, playField.GetWaveParamsForNextLife().SpheroidCount);
    }

    [Fact]
    public void TheGruntLimit_IsRaisedToTheFloor_WhenTheWaveHasPushedItBelowIt()
    {
        var playField = CreateField(new WaveParams(1, 2, GruntMoveDelay: 20, GruntSpeedFloor: 15));
        foreach (var grunt in playField.FieldEntities.Grunts)
        {
            grunt.SpeedUpAsWaveGoesOn(1);
            grunt.SpeedUpAsWaveGoesOn(1);
        }

        Assert.True(playField.FieldEntities.Grunts[0].MoveIntervalMaxBeats < 15);
        Assert.Equal(15, playField.GetWaveParamsForNextLife().GruntMoveDelay); // ROM PLEND2: ROBSPD raised to RMXSPD
    }

    [Fact]
    public void TheGrunts_KeepTheSpeedLimitTheyReached_ButNeverSlowerThanTheWavesFloorAllows()
    {
        var playField = CreateField(new WaveParams(1, 3, GruntMoveDelay: 20, GruntSpeedFloor: 9));
        foreach (var grunt in playField.FieldEntities.Grunts) grunt.SpeedUp(1);

        Assert.Equal(17, playField.GetWaveParamsForNextLife().GruntMoveDelay); // 20 x 7/8, truncated: kept
        Assert.Equal(9, playField.GetWaveParamsForNextLife().GruntSpeedFloor); // the floor goes back to the wave's own
    }

    [Fact]
    public void TheNextLifeIsMadeFromWhatWasLeft()
    {
        var first = CreateField(new WaveParams(1, 5, MikeyCount: 2));
        first.FieldEntities.Grunts[0].Kill();
        first.FieldEntities.Grunts[1].Kill();

        var next = CreateField(first.GetWaveParamsForNextLife());

        Assert.Equal(3, next.FieldEntities.Grunts.Count);
        Assert.Equal(2, next.FieldEntities.FamilyList.Count);
    }

    [Fact]
    public void WhatWasKilledOrRescued_StaysGone_AndWhatIsLeftIsKept()
    {
        var playField = CreateField(new WaveParams(
            5, 4, 3, 2, 1, 2,
            2, 1, 3, 2));
        playField.FieldEntities.Grunts[0].Kill();
        playField.FieldEntities.Electrodes[0].Kill();
        playField.FieldEntities.FamilyList.First(familyMember => familyMember.Kind == FamilyMemberKind.Mommy).Kill();
        playField.FieldEntities.FamilyList.First(familyMember => familyMember.Kind == FamilyMemberKind.Mikey).Kill();
        playField.FieldEntities.Spheroids[0].Kill();

        var left = playField.GetWaveParamsForNextLife();

        Assert.Equal(3, left.GruntCount);
        Assert.Equal(2, left.ElectrodeCount);
        Assert.Equal(1, left.MommyCount);
        Assert.Equal(1, left.DaddyCount);
        Assert.Equal(1, left.MikeyCount);
        Assert.Equal(2, left.HulkCount);
        Assert.Equal(1, left.BrainCount);
        Assert.Equal(2, left.SpheroidCount);
        Assert.Equal(2, left.QuarkCount);
        Assert.Equal(5, left.WaveNumber);
    }

    private static Enforcer CreateEnforcer()
    {
        return new Enforcer(TestEntityParams.Create(new Random(1)), new IntVector2(100, 100), 24);
    }

    private static PlayField CreateField(WaveParams waveParams)
    {
        return new PlayFieldBuilder().WithParameters(waveParams).WithSeed(7).Build();
    }
}
