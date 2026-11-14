namespace Robotron2084.Audio;

/// <summary>The folders of <c>Content/Speech/</c>, one for each robot that speaks.</summary>
/// <remarks>Port only: the arcade has no speech sound chip.</remarks>
public static class SpeechFolders
{
    /// <summary>The folder of Gorf's speech lines.</summary>
    public const string Gorf = "GORF";

    /// <summary>The folder of the Berzerk robot's speech lines: one word in each file.</summary>
    public const string Berzerk = "Berzerk";

    /// <summary>Gets the names of the speech lines in a folder, in the order of <see cref="SpeechLinePaths" />.</summary>
    /// <param name="folder">The folder under <c>Content/Speech/</c>.</param>
    /// <returns>The names, in capitals: each file's name without its extension.</returns>
    public static IReadOnlyList<string> GetLineNames(string folder)
    {
        var folderStart = $"Speech/{folder}/";
        return SpeechLinePaths.All.Where(path => path.StartsWith(folderStart, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path).ToUpperInvariant()).ToList();
    }
}
