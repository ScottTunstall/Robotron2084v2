namespace Robotron2084.Level;

/// <summary>Which game is played: the arcade's own, or the port's with the author's changes.</summary>
/// <remarks>Port only: the arcade has one game.</remarks>
public enum GameStyle
{
    /// <summary>The arcade's game exactly, except that two players can also play at the same time.</summary>
    Classic,

    /// <summary>The game with the author's own changes. For now it plays the same as <see cref="Classic" />.</summary>
    Extended
}
