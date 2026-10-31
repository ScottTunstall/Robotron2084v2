using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>What a death keeps (ROM <c>PLEND</c>, <c>PLSAV</c>, <c>PLRES</c>, notes §134).</summary>
public sealed class WaveSurvivorsTests
{
    private static PlayField CreateField(WaveParameters waveParameters)
    {
        return new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(7).Build();
    }

    private static Enforcer CreateEnforcer()
    {
        return new Enforcer(TestTextures.SharedTextureSet, new IntVector2(100, 100), new Random(1));
    }

    [Fact]
    public void WhatWasKilledOrRescued_StaysGone_AndWhatIsLeftIsKept()
    {
        var playField = CreateField(new WaveParameters(
            5, 4, 3, 2, 1, 2,
            2, 1, 3, 2));
        playField.FieldEntities.Grunts[0].Kill();
        playField.FieldEntities.Electrodes[0].Kill();
        playField.FieldEntities.FamilyList.Members.First(human => human.Kind == HumanKind.Mommy).Kill();
        playField.FieldEntities.FamilyList.Members.First(human => human.Kind == HumanKind.Mikey).Kill();
        playField.FieldEntities.Spheroids[0].Kill();

        var left = WaveSurvivors.GetFrom(playField);

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

    [Fact]
    public void ALifeThatHasBegunToEndIsAlreadyOffTheCount()
    {
        var playField = CreateField(new WaveParameters(1, 2));
        playField.FieldEntities.Grunts[0].Kill(); // dying, not yet gone: the ROM has already counted it off

        Assert.True(playField.FieldEntities.Grunts[0].IsDying() || playField.FieldEntities.Grunts[0].IsDead());
        Assert.Equal(1, WaveSurvivors.GetFrom(playField).GruntCount);
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
        var playField = CreateField(new WaveParameters(1, SpheroidCount: startedWith));
        for (var i = 0; i < startedWith - spheroidsLeft; i++) playField.FieldEntities.Spheroids[i].Kill();

        for (var i = 0; i < enforcers; i++) playField.FieldEntities.Enforcers.Add(CreateEnforcer());

        Assert.Equal(expected, WaveSurvivors.GetFrom(playField).SpheroidCount);
    }

    [Fact]
    public void TheGrunts_KeepTheSpeedLimitTheyReached_ButNeverSlowerThanTheWavesFloorAllows()
    {
        var playField = CreateField(new WaveParameters(1, 3, GruntMoveDelay: 20, GruntSpeedFloor: 9));
        foreach (var grunt in playField.FieldEntities.Grunts) grunt.SpeedUp(1);

        Assert.Equal(17, WaveSurvivors.GetFrom(playField).GruntMoveDelay); // 20 x 7/8, truncated: kept
        Assert.Equal(9, WaveSurvivors.GetFrom(playField).GruntSpeedFloor); // the floor goes back to the wave's own
    }

    [Fact]
    public void TheGruntLimit_IsRaisedToTheFloor_WhenTheWaveHasPushedItBelowIt()
    {
        var playField = CreateField(new WaveParameters(1, 2, GruntMoveDelay: 20, GruntSpeedFloor: 15));
        foreach (var grunt in playField.FieldEntities.Grunts)
        {
            grunt.SpeedUpAsWaveGoesOn(1);
            grunt.SpeedUpAsWaveGoesOn(1);
        }

        Assert.True(playField.FieldEntities.Grunts[0].MoveIntervalMaxBeats < 15);
        Assert.Equal(15, WaveSurvivors.GetFrom(playField).GruntMoveDelay); // ROM PLEND2: ROBSPD raised to RMXSPD
    }

    [Fact]
    public void ATankThatWasAlive_ComesBackAtFullSize_AndOneThatWasKilledDoesNot()
    {
        var playField = CreateField(new WaveParameters(1, TankCount: 3));

        Assert.Equal(3, playField.FieldEntities.Tanks.Count);
        Assert.All(playField.FieldEntities.Tanks, tank => Assert.False(tank.IsGrowing()));

        playField.FieldEntities.Tanks[0].Kill();
        Assert.Equal(2, WaveSurvivors.GetFrom(playField).TankCount);
    }

    [Fact]
    public void TheNextLifeIsMadeFromWhatWasLeft()
    {
        var first = CreateField(new WaveParameters(1, 5, MikeyCount: 2));
        first.FieldEntities.Grunts[0].Kill();
        first.FieldEntities.Grunts[1].Kill();

        var next = CreateField(WaveSurvivors.GetFrom(first));

        Assert.Equal(3, next.FieldEntities.Grunts.Count);
        Assert.Equal(2, next.FieldEntities.FamilyList.Members.Count);
    }
}
