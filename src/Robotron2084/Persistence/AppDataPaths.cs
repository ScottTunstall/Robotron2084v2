namespace Robotron2084.Persistence;

/// <summary>Where the port keeps its files: <c>%LocalAppData%\Robotron2084\</c>.</summary>
public static class AppDataPaths
{
    /// <summary>The path of a file in the port's folder under the user's local application data.</summary>
    /// <param name="fileName">The file's name, on its own: no folder in front of it.</param>
    /// <exception cref="ArgumentException">The name is empty, or has a folder in it, so the path would leave the port's folder.</exception>
    public static string GetFilePath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName != Path.GetFileName(fileName)) throw new ArgumentException("The file name must not have a folder in it.", nameof(fileName));

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Robotron2084",
            fileName);
    }
}
