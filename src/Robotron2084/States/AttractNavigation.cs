namespace Robotron2084.States;

/// <summary>
///     Works out which screen of the attract sequence comes next, and which comes before, for the development keys that
///     step through the whole sequence by hand.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no such keys (notes §97, §149).</item>
///         <item>
///             The steps, in order: the title page on its arcade pane, the title page on the port's pane, the pages of the
///             story (<see cref="StoryPageCount" /> of them), the demo game, and the high score table. After the last
///             step comes the first, and before the first comes the last.
///         </item>
///         <item>
///             A step is a number from 0. This class turns a screen into its step number, and a step number into the
///             screen to open. It does not know about keys.
///         </item>
///         <item>
///             Only this class lists the order of the screens. A screen never names the one before or after it.
///         </item>
///     </list>
/// </remarks>
internal static class AttractNavigation
{
    /// <summary>How many pages the story has.</summary>
    public const int StoryPageCount = 7;

    /// <summary>The step of the title page on the arcade's text pane.</summary>
    public const int TitleArcadePaneStep = 0;

    /// <summary>The step of the title page on the port's text pane.</summary>
    public const int TitlePortPaneStep = 1;

    /// <summary>The step of the first page of the story.</summary>
    public const int FirstStoryPageStep = 2;

    /// <summary>The step of the demo game.</summary>
    public const int DemoStep = FirstStoryPageStep + StoryPageCount;

    /// <summary>The step of the high score table.</summary>
    public const int HighScoresStep = DemoStep + 1;

    /// <summary>How many steps the sequence has.</summary>
    public const int StepCount = HighScoresStep + 1;

    /// <summary>Gets the step after or before a step, wrapping round at either end.</summary>
    /// <param name="step">The step.</param>
    /// <param name="direction">1 for the next step, -1 for the one before.</param>
    public static int GetNeighbourStep(int step, int direction)
    {
        return (step + direction + StepCount) % StepCount;
    }

    /// <summary>Gets the step a screen is on, or null when it is not a screen of the sequence.</summary>
    /// <param name="gameState">The screen.</param>
    public static int? GetStep(IGameState gameState)
    {
        return gameState switch
        {
            TitleScreenState titleScreenState => GetTitleStep(titleScreenState),
            StorylineState storylineState => FirstStoryPageStep + storylineState.GetPage() - 1,
            AttractState => DemoStep,
            WaveClearState { IsAttractMode: true } => DemoStep,
            HighScoreTableState => HighScoresStep,
            _ => null
        };
    }

    /// <summary>Makes the screen for a step.</summary>
    /// <param name="step">The step.</param>
    /// <param name="gameServices">The shared services the screens are made with.</param>
    public static IGameState CreateState(int step, GameServices gameServices)
    {
        if (step == TitleArcadePaneStep) return new TitleScreenState(gameServices, TitleTextPane.Arcade);
        if (step == TitlePortPaneStep) return new TitleScreenState(gameServices, TitleTextPane.Port);
        if (step == DemoStep) return new AttractState(gameServices);
        if (step == HighScoresStep) return new HighScoreTableState(gameServices);

        var storyPage = step - FirstStoryPageStep + 1;
        return new StorylineState(gameServices, storyPage);
    }

    private static int GetTitleStep(TitleScreenState titleScreenState)
    {
        if (titleScreenState.GetShownPane() == TitleTextPane.Port) return TitlePortPaneStep;

        return TitleArcadePaneStep;
    }
}
