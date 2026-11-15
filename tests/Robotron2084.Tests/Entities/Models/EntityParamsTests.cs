using Robotron2084.Entities.Models;
using Robotron2084.Events;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>What every entity is built with.</summary>
public sealed class EntityParamsTests
{
    [Fact]
    public void KeepsWhatItIsGiven()
    {
        var random = new Random(1);

        var entityParams = new EntityParams(TestTextures.SharedTextureSet, NullGameEvents.Instance, random);

        Assert.Same(TestTextures.SharedTextureSet, entityParams.TextureSet);
        Assert.Same(NullGameEvents.Instance, entityParams.GameEvents);
        Assert.Same(random, entityParams.Random);
    }

    [Fact]
    public void RefusesANullTextureSet()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EntityParams(null!, NullGameEvents.Instance, new Random(1)));
    }

    [Fact]
    public void RefusesANullPlaceToAnnounceTo()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EntityParams(TestTextures.SharedTextureSet, null!, new Random(1)));
    }

    [Fact]
    public void RefusesANullRandomSource()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EntityParams(TestTextures.SharedTextureSet, NullGameEvents.Instance, null!));
    }
}
