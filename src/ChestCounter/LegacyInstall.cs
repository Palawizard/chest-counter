using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace ChestCounter;

// Versions 1.x were installed per user by Inno Setup and cannot be updated by Velopack.
// Their uninstaller only removes the program folder and shortcuts; saved results live elsewhere.
internal static class LegacyInstall
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{7C1E2B64-3D0F-4A59-9E21-6B8F4D2A1C37}_is1";

    // Velopack gives install and update hooks 30 seconds before it gives up on them.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
        if (key?.GetValue("UninstallString") is not string command)
        {
            return;
        }

        var uninstaller = command.Trim('"');
        if (!File.Exists(uninstaller))
        {
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(uninstaller, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = false });
            process?.WaitForExit(Timeout);
        }
        catch (Win32Exception)
        {
            // The old version stays installed; the user can still remove it from Windows settings.
        }
    }
}
