using Robotron2084.Core.Models;
using Robotron2084.Entities;
using Robotron2084.Entities.Collections;
using Robotron2084.Events;
using Xunit;

namespace Robotron2084.Tests.Entities.Collections;

/// <summary>What every kind's list can count and answer, shown with electrodes, which take a while to die.</summary>
public sealed class EntityListTests
{
    [Fact]
    public void ADyingEntityIsStillOnTheField_ButNoLongerAlive()
    {
        var electrodes = CreateTwoElectrodes();

        electrodes[0].Kill(); // an electrode shrivels: dying, not yet dead

        Assert.Equal(2, electrodes.CountOnField());
        Assert.Equal(1, electrodes.CountAlive());
    }

    [Fact]
    public void AListIsAllDeadOnlyWhenNothingInItIsAliveOrDying()
    {
        var electrodes = CreateTwoElectrodes();
        Assert.False(electrodes.AreAllDead());

        electrodes[0].Kill();
        electrodes[1].Kill();

        Assert.False(electrodes.AreAllDead()); // still shrivelling
        Assert.True(new ElectrodeList().AreAllDead());
    }

    [Fact]
    public void AnyAliveIsTrueUntilTheLastOneStartsToDie()
    {
        var electrodes = CreateTwoElectrodes();
        Assert.True(electrodes.AnyAlive());

        electrodes[0].Kill();
        Assert.True(electrodes.AnyAlive());

        electrodes[1].Kill();
        Assert.False(electrodes.AnyAlive());
    }

    private static ElectrodeList CreateTwoElectrodes()
    {
        return
        [
            new Electrode(TestTextures.SharedTextureSet, NullGameEvents.Instance, new IntVector2(10, 10)),
            new Electrode(TestTextures.SharedTextureSet, NullGameEvents.Instance, new IntVector2(50, 10))
        ];
    }
}
