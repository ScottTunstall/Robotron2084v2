namespace RobotronSoundPlayer;

/// <summary>Lists the sounds and asks which to play, over and over, until the user quits.</summary>
internal static class InteractiveMenu
{
    /// <summary>Shows the list, then plays each choice until the user types <c>q</c>.</summary>
    /// <param name="catalog">What can be played.</param>
    /// <param name="playback">Plays a choice.</param>
    /// <param name="seconds">How long to play each one, or null to stop when it goes quiet.</param>
    public static void Run(SoundCatalog catalog, SoundPlayback playback, double? seconds)
    {
        CatalogPrinter.Print(catalog);
        while (true)
        {
            Console.WriteLine();
            Console.Write("Play which? (a name, a number from the list, $00-$3F, 'list', or 'q' to quit): ");
            var answer = Console.ReadLine()?.Trim();
            if (answer is null || IsQuit(answer)) return;

            Answer(answer, catalog, playback, seconds);
        }
    }

    /// <summary>Acts on one answer: shows the list again, plays a sound, or says nothing matched.</summary>
    /// <param name="answer">What the user typed.</param>
    /// <param name="catalog">What can be played.</param>
    /// <param name="playback">Plays a choice.</param>
    /// <param name="seconds">How long to play, or null to stop when it goes quiet.</param>
    private static void Answer(string answer, SoundCatalog catalog, SoundPlayback playback, double? seconds)
    {
        if (answer.Length == 0) return;

        if (answer.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            CatalogPrinter.Print(catalog);
            return;
        }

        var request = catalog.Find(answer);
        if (request is null)
        {
            Console.WriteLine($"There is no sound called '{answer}'. Type 'list' to see them all.");
            return;
        }

        playback.PlayToSpeakers(request, seconds);
    }

    /// <summary>True when the answer asks to quit.</summary>
    /// <param name="answer">What the user typed.</param>
    private static bool IsQuit(string answer)
    {
        return answer.Equals("q", StringComparison.OrdinalIgnoreCase) ||
               answer.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }
}
