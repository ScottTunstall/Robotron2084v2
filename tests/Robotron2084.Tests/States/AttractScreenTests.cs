using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>
///     The attract cycle and its keys. <c>RobotronGame.Update</c> reads F1, F2 and F3 (which start a game), Enter (which
///     opens the menu page) and the coin key
///     on EVERY screen of the attract cycle — the author's question of
///     2026-09-20 — and it recognises those screens by the <see cref="IAttractState" /> marker,
///     so a screen that joins the cycle without the marker silently loses the keys.
///     F10 is NOT one of those keys: it opens the DEVELOPER TOOLS page from EVERY screen, attract
///     screens and games alike, so it is not what the marker is for.
///     That is what these tests guard, from both ends: the marker covers exactly the four
///     attract screens, and EVERY <see cref="IGameState" /> in the assembly is either one of
///     them or named in <see cref="NotAttractScreens" /> — so adding a screen forces the
///     decision instead of quietly falling outside the key handling.
/// </summary>
public sealed class AttractScreenTests
{
    /// <summary>The four screens of the attract cycle, in no particular order.</summary>
    private static readonly Type[] AttractScreens =
    [
        typeof(TitleScreenState),
        typeof(StorylineState),
        typeof(AttractState),
        typeof(HighScoreTableState)
    ];

    /// <summary>
    ///     The screens that are NOT attract screens: where a game is being played (or paused in
    ///     front of), the arcade's own rule applies instead — no key restarts the machine under a
    ///     player mid-game — and so do the screens that finish a game, which the cabinet also runs
    ///     outside the attract cycle: the game-over message, the CONG initials entry (notes §116) and
    ///     the ONLY5P page, plus the port-only definitions and GAME ADJUSTMENT pages, the sprite test, the sound test and
    ///     the speech test, and the DEVELOPER TOOLS page — which is opened by F10 from every screen, so it deliberately
    ///     does NOT take the attract cycle's keys (its own number keys choose an option), and the menu page, whose own
    ///     keys choose its lines.
    /// </summary>
    private static readonly Type[] NotAttractScreens =
    [
        typeof(PlayingState),
        typeof(WaveClearState),
        typeof(GameOverState),
        typeof(InitialsEntryState),
        typeof(EntriesMaximumState),
        typeof(DefineInputsState),
        typeof(SettingsState),
        typeof(MainMenuState),
        typeof(SpriteTestState),
        typeof(SoundTestState),
        typeof(SpeechTestState),
        typeof(DeveloperToolsState)
    ];

    [Fact]
    public void TheStartKeysAreLiveOnExactlyTheFourAttractScreens()
    {
        var marked = Screens()
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
        var unexplained = Screens()
            .Except(AttractScreens)
            .Except(NotAttractScreens)
            .ToArray();

        Assert.True(
            unexplained.Length == 0,
            $"new screen(s) {string.Join(", ", unexplained.Select(screen => screen.Name))} — add them to "
            + $"{nameof(AttractScreens)} (they take the attract keys, so they also need IAttractState) or to "
            + $"{nameof(NotAttractScreens)} (they do not)");
    }

    /// <summary>Every concrete <see cref="IGameState" /> in the game assembly.</summary>
    private static Type[] Screens()
    {
        return typeof(IGameState).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IGameState).IsAssignableFrom(type))
            .ToArray();
    }
}
