namespace Robotron2084.States;

/// <summary>
///     A screen in the attract cycle — the title, the storyline movie, the phony-player
///     demo and the high score table. Port-only marker (notes §101): the shell reads the
///     game-start keys (F1 / F2 / F3) on ANY of these, so a game can be
///     started without waiting out the attract sequence. (F10 is not one of them: the DEVELOPER
///     TOOLS page it opens is reachable from every screen, attract and game alike.)
/// </summary>
public interface IAttractState
{
}
