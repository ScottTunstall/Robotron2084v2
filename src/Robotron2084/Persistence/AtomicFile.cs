using System.Text;

namespace Robotron2084.Persistence;

/// <summary>Writes a saved file so that it is never left half written: the old file stays until the new one is complete.</summary>
public static class AtomicFile
{
    /// <summary>The ending of the name of the file the new text is written to first.</summary>
    private const string TemporaryFileEnding = ".tmp";

    /// <summary>
    ///     Writes text to a file. It is written to a file beside it first, and that file then takes the place of the old one,
    ///     so a crash or a power cut part way through leaves the old file as it was.
    /// </summary>
    /// <param name="path">The file to write. Its folder is made when it is not there.</param>
    /// <param name="text">The text.</param>
    /// <param name="encoding">How the text is turned into bytes.</param>
    /// <exception cref="PersistenceException">The file could not be written.</exception>
    public static void WriteAllText(string path, string text, Encoding encoding)
    {
        var temporaryPath = path + TemporaryFileEnding;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporaryPath, text, encoding);
            File.Move(temporaryPath, path, true);
        }
        catch (IOException exception)
        {
            throw new PersistenceException($"The file {path} could not be saved: {exception.Message}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PersistenceException($"The file {path} could not be opened for saving: {exception.Message}", exception);
        }
    }
}
