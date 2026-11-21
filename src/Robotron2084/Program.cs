using System.Runtime.InteropServices;
using Robotron2084;
using Robotron2084.Persistence;

try
{
    using var robotronGame = new RobotronGame();
    robotronGame.Run();
}
catch (PersistenceException persistenceException)
{
    NativeMessageBox.Show(persistenceException.Message);
}
catch (Exception exception)
{
    // Anything else is a failure nobody planned for: leave a note and tell the player, then let it end the game as before.
    CrashLog.Write(exception);
    NativeMessageBox.Show($"Robotron 2084 has stopped because of an unexpected error.{Environment.NewLine}{Environment.NewLine}" +
                          $"{exception.Message}{Environment.NewLine}{Environment.NewLine}Details are in {CrashLog.Path}");
    throw;
}

/// <summary>
///     The one native dialog the game shows: a modal error box, for a saved file that exists but could not be read
///     (ERR-1) and for an unexpected failure. Direct user32, as <see cref="Robotron2084.Core.DisplayInfo" />
///     already is — no UI framework reference. The game is for Windows only (its manifest says so), so this is the
///     platform's own dialog.
/// </summary>
internal static class NativeMessageBox
{
    private const uint MbIconError = 0x00000010;

    public static void Show(string message)
    {
        MessageBoxW(IntPtr.Zero, message, "Robotron 2084", MbIconError);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
