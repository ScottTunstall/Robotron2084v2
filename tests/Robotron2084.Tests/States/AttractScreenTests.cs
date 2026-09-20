using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>
/// The attract cycle and the author's start keys (notes §101). <c>RobotronGame.Update</c>
/// reads F1 (one player) / F2 (two players, alternating) / F3 (two players, simultaneous)
/// and F10 (DEFINE INPUTS) on EVERY screen of the attract cycle — the author's question of
/// 2026-09-20 — and it recognises those screens by the <see cref="IAttractState"/> marker,
/// so a screen that joins the cycle without the marker silently loses the keys.
///
/// That is what these tests guard, from both ends: the marker covers exactly the four
/// attract screens, and EVERY <see cref="IGameState"/> in the assembly is either one of
/// them or named in <see cref="NotAttractScreens"/> — so adding a screen forces the
/// decision instead of quietly falling outside the key handling.
/// </summary>
public sealed class AttractScreenTests
{
    /// <summary>The four screens of the attract cycle, in no particular order.</summary>
    private static readonly Type[] AttractScreens =
    [
        typeof(TitleScreenState),
        typeof(StorylineState),
        typeof(AttractState),
        typeof(HighScoreTableState),
    ];

    /// <summary>
    /// The screens that are NOT attract screens: where a game is being played (or paused in
    /// front of), the arcade's own rule applies instead — no key restarts the machine under a
    /// player mid-game — plus the port-only definitions page.
    /// </summary>
    private static readonly Type[] NotAttractScreens =
    [
        typeof(PlayingState),
        typeof(WaveClearState),
        typeof(GameOverState),
        typeof(DefineInputsState),
    ];

    [Fact]
    public void TheStartKeysAreLiveOnExactlyTheFourAttractScreens()
    {
        string[] marked = Screens()
            .Where(typeof(IAttractState).IsAssignableFrom)
            .Select(screen => screen.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            AttractScreens.Select(screen => screen.Name).OrderBy(name => name),
            marked);
    }

    [Fact]
    public void EveryScreenIsEitherAnAttractScreenOrDeliberatelyNotOne()
    {
        Type[] unexplained = Screens()
            .Except(AttractScreens)
            .Except(NotAttractScreens)
            .ToArray();

        Assert.True(
            unexplained.Length == 0,
            $"new screen(s) {string.Join(", ", unexplained.Select(screen => screen.Name))} — add them to "
            + $"{nameof(AttractScreens)} (they take F1/F2/F3/F10, so they also need IAttractState) or to "
            + $"{nameof(NotAttractScreens)} (they do not)");
    }

    /// <summary>Every concrete <see cref="IGameState"/> in the game assembly.</summary>
    private static Type[] Screens() =>
        typeof(IGameState).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IGameState).IsAssignableFrom(type))
            .ToArray();
}
