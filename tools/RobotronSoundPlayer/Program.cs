using Microsoft.Xna.Framework.Audio;

namespace RobotronSoundPlayer;

/// <summary>
///     Robotron sound player: plays the arcade's sounds by running the real sound ROM on an emulated sound
///     board. Run it with no words for a menu.
/// </summary>
internal static class Program
{
    /// <summary>The exit code for success.</summary>
    private const int Success = 0;

    /// <summary>The exit code for a command line that could not be acted on.</summary>
    private const int BadCommandLine = 1;

    /// <summary>The exit code for a missing sound ROM or a computer with no sound output.</summary>
    private const int CannotPlay = 2;

    /// <summary>Reads the command line and does what it asks.</summary>
    /// <param name="args">The words after the player's name.</param>
    /// <returns>An exit code.</returns>
    private static int Main(string[] args)
    {
        var commandLine = new CommandLine(args);
        if (commandLine.Error is not null) return Fail(commandLine.Error);

        switch (commandLine.Command)
        {
            case "help" or "-h" or "-?" or "/?":
                PrintUsage();
                return Success;
            case "list":
                CatalogPrinter.Print(new SoundCatalog());
                return Success;
            case null or "play":
                return PlayOrShowMenu(commandLine);
            default:
                return Fail($"Unknown command '{commandLine.Command}'.");
        }
    }

    /// <summary>Plays the sound the command line names, or shows the menu when it names none.</summary>
    /// <param name="commandLine">The command line.</param>
    /// <returns>An exit code.</returns>
    private static int PlayOrShowMenu(CommandLine commandLine)
    {
        var catalog = new SoundCatalog();
        ISoundRequest? request = null;
        if (commandLine.Command == "play")
        {
            request = catalog.Find(commandLine.Choice ?? string.Empty);
            if (request is null)
                return Fail(
                    $"There is no sound called '{commandLine.Choice}'. Run 'RobotronSoundPlayer list' to see them all.");
        }

        var romPath = SoundRomLocator.Find(commandLine.RomPath);
        if (romPath is null)
        {
            Console.Error.WriteLine(
                $"The sound ROM ({SoundRomLocator.RomFileName}) was not found. Copy it into ref/rom, or pass --rom <file>.");
            return CannotPlay;
        }

        return Play(catalog, request, commandLine, new SoundPlayback(File.ReadAllBytes(romPath)));
    }

    /// <summary>Plays one sound, or runs the menu when there is none.</summary>
    /// <param name="catalog">What can be played.</param>
    /// <param name="request">The sound to play, or null for the menu.</param>
    /// <param name="commandLine">The command line, for its options.</param>
    /// <param name="playback">Plays a sound.</param>
    /// <returns>An exit code.</returns>
    private static int Play(SoundCatalog catalog, ISoundRequest? request, CommandLine commandLine,
        SoundPlayback playback)
    {
        if (request is not null && commandLine.WavPath is not null)
        {
            playback.WriteToFile(request, commandLine.Seconds, commandLine.WavPath);
            return Success;
        }

        try
        {
            if (request is null)
            {
                InteractiveMenu.Run(catalog, playback, commandLine.Seconds);
                return Success;
            }

            playback.PlayToSpeakers(request, commandLine.Seconds);
            return Success;
        }
        catch (NoAudioHardwareException)
        {
            Console.Error.WriteLine(
                "This computer has no sound output. Use --wav <file> to write the sound to a file instead.");
            return CannotPlay;
        }
    }

    /// <summary>Says what was wrong, then how to use the player.</summary>
    /// <param name="message">What was wrong.</param>
    /// <returns>The exit code for a bad command line.</returns>
    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
        PrintUsage();
        return BadCommandLine;
    }

    /// <summary>Prints how to use the player.</summary>
    private static void PrintUsage()
    {
        Console.WriteLine("""
                          Robotron sound player: plays the arcade's sounds on an emulated sound board running the real
                          sound ROM.

                          Usage:
                            RobotronSoundPlayer                      a menu: pick sounds to play, one after another
                            RobotronSoundPlayer list                 list every sound
                            RobotronSoundPlayer play <sound>         play one sound, then stop

                          <sound> is a name from the list (WaveEnd, Laser, Transporter ...), its number in the
                          list, or one of the board's sound numbers written $0E, 0x0E or #14.

                          Options:
                            --seconds <n>   play for exactly n seconds (otherwise: until it goes quiet, at most 15 s)
                            --wav <file>    write the sound to a WAV file instead of playing it
                            --rom <file>    the sound ROM (otherwise: looked for in ref/rom)

                          Press any key while a sound plays to stop it.
                          """);
    }
}
