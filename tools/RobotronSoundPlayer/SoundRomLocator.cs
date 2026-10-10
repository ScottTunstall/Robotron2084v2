namespace RobotronSoundPlayer;

/// <summary>
///     Finds the sound board's ROM, which is not in git: beside the player, or in <c>ref/rom</c> above it or above
///     the current folder.
/// </summary>
internal static class SoundRomLocator
{
    /// <summary>The sound ROM's file name in MAME's <c>robotron</c> set.</summary>
    public const string RomFileName = "video_sound_rom_3_std_767.ic12";

    /// <summary>Finds the ROM.</summary>
    /// <param name="givenPath">A path the user gave, which is used when it exists.</param>
    /// <returns>The ROM's path, or null when it is nowhere to be found.</returns>
    public static string? Find(string? givenPath)
    {
        if (givenPath is not null) return File.Exists(givenPath) ? givenPath : null;

        var besideThePlayer = Path.Combine(AppContext.BaseDirectory, "Roms", RomFileName);
        if (File.Exists(besideThePlayer)) return besideThePlayer;

        return FindInRefRomAbove(AppContext.BaseDirectory) ?? FindInRefRomAbove(Environment.CurrentDirectory);
    }

    /// <summary>Looks for <c>ref/rom</c> in a folder and in each folder above it.</summary>
    /// <param name="start">The folder to start in.</param>
    /// <returns>The ROM's path, or null when no folder has it.</returns>
    private static string? FindInRefRomAbove(string start)
    {
        for (DirectoryInfo? folder = new(start); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, "ref", "rom", RomFileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
