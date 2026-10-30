namespace Robotron2084.Entities;

/// <summary>An entity that can be taken off the field at once.</summary>
public interface IRemovable
{
    /// <summary>Takes it off the field. It leaves nothing behind, such as an explosion or points.</summary>
    void Kill();
}
