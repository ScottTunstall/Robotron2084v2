namespace Robotron2084.Entities;

/// <summary>An entity that can be taken off the field at once — by a laser, or by the player's touch. What it leaves behind is the caller's business.</summary>
/// <remarks>ROM: RRS22's <c>KILROB</c>/<c>KILLOF</c>, which free the object; the explosion, the burst and the score are the killer's own calls (notes §61, §64).</remarks>
public interface IRemovable
{
    /// <summary>Takes it off the field.</summary>
    void Kill();
}
