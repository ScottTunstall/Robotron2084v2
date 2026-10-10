namespace Robotron2084.Persistence;

/// <summary>Where the port keeps its files: <c>%LocalAppData%\Robotron2084\</c>.</summary>
public static class AppDataPaths
{
    /// <summary>The path of a file in the port's folder under the user's local application data.</summary>
    /// <param name="fileName">The file's name.</param>
    public static string GetFilePath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Robotron2084",
            fileName);
    }
}
