using System.Diagnostics;
using Microsoft.Win32;

namespace ChestCounter;

// Versions 1.x were installed per user by Inno Setup and cannot be updated by Velopack.
// Their uninstaller only removes the program folder and shortcuts; saved results live elsewhere.
internal static class LegacyInstall
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{7C1E2B64-3D0F-4A59-9E21-6B8F4D2A1C37}_is1";

    // Velopack gives install and update hooks 30 seconds before it gives up on them.
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan UninstallTimeout = TimeSpan.FromSeconds(20);

    // Never throws: a failed cleanup must not fail the install or update that called it.
    public static void Remove()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            if (key?.GetValue("UninstallString") is not string command || !File.Exists(command.Trim('"')))
            {
                return;
            }

            var uninstaller = command.Trim('"');
            CloseRunning(Path.GetDirectoryName(uninstaller)!);
            using var process = Process.Start(new ProcessStartInfo(uninstaller, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = false });
            process?.WaitForExit(UninstallTimeout);
        }
        catch (Exception)
        {
            // The old version stays installed; the user can still remove it from Windows settings.
        }
    }

    // A running 1.x locks its files, so its uninstaller would leave it behind. It saves after
    // every change, so stopping it loses nothing.
    private static void CloseRunning(string installFolder)
    {
        foreach (var process in Process.GetProcessesByName("ChestCounter"))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName.StartsWith(installFolder, StringComparison.OrdinalIgnoreCase) != true)
                    {
                        continue;
                    }

                    process.CloseMainWindow();
                    if (!process.WaitForExit(CloseTimeout))
                    {
                        process.Kill();
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Already gone, or not ours to inspect.
                }
            }
        }
    }
}
