namespace Robotron2084.Events;

/// <summary>Something that announces what happens to it. Until it is given somewhere to announce to, it announces to nobody.</summary>
public interface IRaisesGameEvents
{
    /// <summary>Where this announces what happens to it.</summary>
    IGameEvents GameEvents { get; set; }
}
