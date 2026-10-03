namespace RobotronSoundPlayer;

/// <summary>Prints the catalog: the game's sounds, numbered, and the board's sound numbers in rows.</summary>
internal static class CatalogPrinter
{
    /// <summary>Sound numbers printed on each row.</summary>
    private const int SoundNumbersPerRow = 16;

    /// <summary>The width the game sounds' names are padded to.</summary>
    private const int NameWidth = 26;

    /// <summary>Prints the catalog to the console.</summary>
    /// <param name="catalog">The catalog.</param>
    public static void Print(SoundCatalog catalog)
    {
        Console.WriteLine("The game's sounds (pick by name or number):");
        for (int i = 0; i < catalog.GameSounds.Count; i++)
        {
            ISoundRequest sound = catalog.GameSounds[i];
            Console.WriteLine($"  {i + 1,3}  {sound.Name.PadRight(NameWidth)}{sound.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("The board's sound numbers on their own (pick as $00 to $3F):");
        foreach (ISoundRequest[] row in catalog.SoundNumbers.Chunk(SoundNumbersPerRow))
        {
            Console.WriteLine("  " + string.Join(" ", row.Select(sound => sound.Name)));
        }
    }
}
