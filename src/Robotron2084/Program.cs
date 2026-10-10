using System.Runtime.InteropServices;
using Robotron2084;
using Robotron2084.Persistence;

try
{
    using var game = new RobotronGame();
    game.Run();
}
catch (PersistenceException exception)
{
    NativeMessageBox.Show(exception.Message);
}

/// <summary>
///     The one native dialog the game shows: a modal error box for a saved file that
///     exists but could not be read (ERR-1). Direct user32, as <see cref="DisplayInfo" />
///     already is — no UI framework reference.
/// </summary>
internal static class NativeMessageBox
{
    private const uint MbIconError = 0x00000010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static void Show(string message)
    {
        MessageBoxW(IntPtr.Zero, message, "Robotron 2084", MbIconError);
    }
}
