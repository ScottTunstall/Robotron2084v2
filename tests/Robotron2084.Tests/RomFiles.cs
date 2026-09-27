using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Finds the ROM images in <c>ref/rom</c>. They are not in git (notes §126), so a test that needs one
/// skips when it is missing.
/// </summary>
internal static class RomFiles
{
    /// <summary>The main board's 64K image.</summary>
    public const string MainRom = "robotron64k.bin";

    /// <summary>The sound board's 4K ROM.</summary>
    public const string SoundRom = "video_sound_rom_3_std_767.ic12";

    /// <summary>Reads a ROM image, or skips the test when it is not there.</summary>
    /// <param name="fileName">The image's file name in <c>ref/rom</c>.</param>
    /// <returns>The image's bytes.</returns>
    public static byte[] ReadOrSkip(string fileName)
    {
        string? path = Find(fileName);
        Assert.SkipWhen(path is null, $"{fileName} is not in ref/rom.");
        return File.ReadAllBytes(path!);
    }

    private static string? Find(string fileName)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "ref", "rom", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
