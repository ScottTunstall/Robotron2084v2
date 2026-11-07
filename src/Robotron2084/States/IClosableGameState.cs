namespace Robotron2084.States;

/// <summary>A screen that changes something other screens share, such as the palette, and puts it back when it is left.</summary>
public interface IClosableGameState : IGameState
{
    /// <summary>
    ///     Puts back everything the screen changed that other screens share. It is called once, when another screen takes
    ///     over, however the screen is left.
    /// </summary>
    void Close();
}
