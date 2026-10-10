namespace Robotron2084.Entities;

/// <summary>An entity that can be taken off the field at once, by a laser or by the player's touch. Whatever it leaves behind, such as an explosion or points, is up to whoever killed it.</summary>
/// <remarks>ROM: RRS22's <c>KILROB</c>/<c>KILLOF</c>, which free the object. The explosion, the burst and the score are made by the killer's own calls (notes §61, §64).</remarks>
public interface IRemovable
{
    /// <summary>Takes it off the field.</summary>
    void Kill();
}
