using System.Runtime.InteropServices;

namespace ChestCounter;

// Shortcuts keep pointing at the same exe path across versions, so Windows keeps showing the
// cached icon of an older version in the taskbar until the shell is told that icons changed.
internal static class ShellIcons
{
    private const int AssociationsChanged = 0x08000000;
    private const uint Flush = 0x1000;

    public static void Refresh() => SHChangeNotify(AssociationsChanged, Flush, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
