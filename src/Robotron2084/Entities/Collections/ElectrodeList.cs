using Microsoft.Xna.Framework;

namespace Robotron2084.Entities.Collections;

/// <summary>The electrodes on the field.</summary>
public sealed class ElectrodeList : EntityList<Electrode>
{
    /// <summary>Says whether no electrode touches a box, so that something can be put there.</summary>
    /// <param name="bounds">The box to test, in port pixels.</param>
    /// <returns>True when the box is clear of every electrode.</returns>
    public bool IsClearOf(Rectangle bounds)
    {
        return this.All(electrode => !electrode.GetBounds().Intersects(bounds));
    }
}
