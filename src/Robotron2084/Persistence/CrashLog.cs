namespace Robotron2084.Persistence;

/// <summary>Keeps a note of an unexpected failure, in the port's folder under the user's local application data, so it can be looked at afterwards.</summary>
public static class CrashLog
{
    private static readonly string FilePath = AppDataPaths.GetFilePath("crash.log");

    /// <summary>The path of the log file.</summary>
    public static string Path => FilePath;

    /// <summary>Adds the failure to the end of the log. If the log cannot be written, nothing is done: it must never hide the failure itself.</summary>
    /// <param name="exception">What went wrong.</param>
    public static void Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{DateTimeOffset.Now:u}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // The failure that is being logged matters more than a log that cannot be written.
        }
        catch (UnauthorizedAccessException)
        {
            // As above: a folder that cannot be written to must not hide the failure.
        }
    }
}
