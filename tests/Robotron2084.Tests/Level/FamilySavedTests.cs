using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level.Models;
using Robotron2084.Level;
using Robotron2084.Score;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>Counting the family members lost on a field, and the player's flag that says the whole family was saved.</summary>
public sealed class FamilySavedTests
{
    private static PlayField CreateFieldWithFamily()
    {
        return new PlayFieldBuilder().WithParameters(new WaveParams(1, MikeyCount: 1, MommyCount: 1, DaddyCount: 1)).WithSeed(1)
            .Build();
    }

    [Fact]
    public void AFreshFieldHasLostNoOne_AndHasTheWholeFamilyLeft()
    {
        var playField = CreateFieldWithFamily();

        Assert.Equal(0, playField.GetFamilyMembersLost());
        Assert.True(playField.FieldEntities.FamilyList.AnyAlive());
    }

    [Fact]
    public void AFamilyMemberKilledIsLost()
    {
        var playField = CreateFieldWithFamily();

        playField.FieldEntities.FamilyList.First().Kill();

        Assert.Equal(1, playField.GetFamilyMembersLost());
    }

    [Fact]
    public void AFamilyMemberRescuedIsNotLost()
    {
        var playField = CreateFieldWithFamily();
        var familyMember = playField.FieldEntities.FamilyList.First();

        familyMember.Rescue();
        playField.CountRescuedFamilyMembers();

        Assert.Equal(0, playField.GetFamilyMembersLost());
    }

    [Fact]
    public void WhenEveryFamilyMemberIsRescued_NoneIsLeft_AndNoneIsLost()
    {
        var playField = CreateFieldWithFamily();

        foreach (var familyMember in playField.FieldEntities.FamilyList.ToList())
        {
            familyMember.Rescue();
            playField.CountRescuedFamilyMembers();
        }

        Assert.False(playField.FieldEntities.FamilyList.AnyAlive());
        Assert.Equal(0, playField.GetFamilyMembersLost());
    }

    private static PlayerSlot CreatePlayerSlot()
    {
        return new PlayerSlot(1, new FakeInputSource(), new LivesCounter(3), 1, new ScoreBoard(0));
    }

    [Fact]
    public void APlayerStartsTheirFirstWaveNotAChicken()
    {
        var playerSlot = CreatePlayerSlot();

        Assert.False(playerSlot.IsChicken);
        Assert.Equal(0, playerSlot.FamilyMembersLostThisWave);
    }

    [Fact]
    public void APlayerWhoDoesNotRescueTheWholeFamily_IsAChicken_AndTheCountOfLostFamilyStartsAgain()
    {
        var playerSlot = CreatePlayerSlot();
        playerSlot.FamilyMembersLostThisWave = 2;

        playerSlot.RecordWaveCleared(false);

        Assert.True(playerSlot.IsChicken);
        Assert.Equal(0, playerSlot.FamilyMembersLostThisWave);
    }

    [Fact]
    public void AChicken_StaysAChicken_UntilTheyRescueTheWholeFamily()
    {
        var playerSlot = CreatePlayerSlot();

        playerSlot.RecordWaveCleared(false);
        playerSlot.RecordWaveCleared(false);
        Assert.True(playerSlot.IsChicken);

        playerSlot.RecordWaveCleared(true);
        Assert.False(playerSlot.IsChicken);
    }
}
