using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;
using Robotron2084.Entities;
using Robotron2084.Entities.Collections;
using Robotron2084.Events;
using Xunit;

namespace Robotron2084.Tests.Entities.Collections;

/// <summary>The electrodes on the field.</summary>
public sealed class ElectrodeListTests
{
    [Fact]
    public void ABoxIsClearOnlyWhenNoElectrodeTouchesIt()
    {
        var electrode = new Electrode(TestTextures.SharedTextureSet, NullGameEvents.Instance, new IntVector2(100, 100));
        ElectrodeList electrodes = [electrode];

        Assert.False(electrodes.IsClearOf(electrode.GetBounds()));
        Assert.True(electrodes.IsClearOf(new Rectangle(400, 400, 10, 10)));
        Assert.True(new ElectrodeList().IsClearOf(electrode.GetBounds()));
    }
}
