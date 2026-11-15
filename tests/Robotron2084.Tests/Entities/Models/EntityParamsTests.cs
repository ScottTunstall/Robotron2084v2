using Robotron2084.Entities.Models;
using Robotron2084.Events;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>What every entity is built with.</summary>
public sealed class EntityDependenciesTests
{
    [Fact]
    public void KeepsWhatItIsGiven()
    {
        var random = new Random(1);

        var dependencies = new EntityDependencies(TestTextures.SharedTextureSet, NullGameEvents.Instance, random);

        Assert.Same(TestTextures.SharedTextureSet, dependencies.TextureSet);
        Assert.Same(NullGameEvents.Instance, dependencies.GameEvents);
        Assert.Same(random, dependencies.Random);
    }

    [Fact]
    public void RefusesANullTextureSet()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EntityDependencies(null!, NullGameEvents.Instance, new Random(1)));
    }

    [Fact]
    public void RefusesANullPlaceToAnnounceTo()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EntityDependencies(TestTextures.SharedTextureSet, null!, new Random(1)));
    }

    [Fact]
    public void RefusesANullRandomSource()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EntityDependencies(TestTextures.SharedTextureSet, NullGameEvents.Instance, null!));
    }
}
