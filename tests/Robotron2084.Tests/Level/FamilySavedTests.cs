using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>Counting the family members lost on a field, and the player's flag that says the whole family was saved.</summary>
public sealed class FamilySavedTests
{
    private static PlayField CreateFieldWithFamily()
    {
        return new PlayFieldBuilder().WithParameters(new WaveParameters(1, MikeyCount: 1, MommyCount: 1, DaddyCount: 1)).WithSeed(1)
            .Build();
    }

    [Fact]
    public void AFreshFieldHasLostNoOne_AndHasTheWholeFamilyLeft()
    {
        var playField = CreateFieldWithFamily();

        Assert.Equal(0, playField.GetFamilyMembersLost());
        Assert.True(playField.HasFamilyMemberLeft());
    }

    [Fact]
    public void AFamilyMemberKilledIsLost()
    {
        var playField = CreateFieldWithFamily();

        playField.FieldEntities.FamilyList.Members.First().Kill();

        Assert.Equal(1, playField.GetFamilyMembersLost());
    }

    [Fact]
    public void AFamilyMemberRescuedIsNotLost()
    {
        var playField = CreateFieldWithFamily();
        var familyMember = playField.FieldEntities.FamilyList.Members.First();

        familyMember.Rescue();
        playField.CountRescuedFamilyMembers();

        Assert.Equal(0, playField.GetFamilyMembersLost());
    }

    [Fact]
    public void WhenEveryFamilyMemberIsRescued_NoneIsLeft_AndNoneIsLost()
    {
        var playField = CreateFieldWithFamily();

        foreach (var familyMember in playField.FieldEntities.FamilyList.Members.ToList())
        {
            familyMember.Rescue();
            playField.CountRescuedFamilyMembers();
        }

        Assert.False(playField.HasFamilyMemberLeft());
        Assert.Equal(0, playField.GetFamilyMembersLost());
    }

    [Fact]
    public void APlayerStartsTheirFirstWaveHavingSavedTheWholeFamily()
    {
        var playerSlot = new PlayerSlot(1, new FakeInputSource(), 3, 1);

        Assert.True(playerSlot.SavedWholeFamilyLastWave);
        Assert.Equal(0, playerSlot.FamilyMembersLostThisWave);
    }
}
