using System.Diagnostics;
using Microsoft.Win32;

namespace ChestCounter;

// "Open with Fortnite": a per-user Run value starts a small standby process with Windows. It sleeps at
// low priority, looks for Fortnite every few seconds and starts Chest Counter quietly when it appears.
// No administrator rights, no scheduled task; uninstalling removes the value.
internal static class StartWithFortnite
{
    public const string Argument = "--standby";
    public const string FortniteProcess = "FortniteClient-Win64-Shipping";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Chest Counter";
    private const string StandbyName = @"Local\Palawi.ChestCounter.Standby";
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    public static bool IsOn
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static string Command(string executable) => $"\"{executable}\" {Argument}";

    public static void Set(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on)
        {
            key.SetValue(ValueName, Command(Environment.ProcessPath!));
            StartStandby();
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    // After an update or a reinstall the standby process is gone until the next sign-in: start it again.
    public static void Refresh()
    {
        if (IsOn)
        {
            Set(true);
        }
    }

    public static bool IsFortniteRunning()
    {
        var processes = Process.GetProcessesByName(FortniteProcess);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    public static int RunStandby()
    {
        using var standby = new Mutex(initiallyOwned: true, StandbyName, out var created);
        if (!created)
        {
            return 0;
        }

        NativeMethods.RunInBackground();
        while (IsOn)
        {
            if (IsFortniteRunning())
            {
                if (!SingleInstance.IsWindowRunning)
                {
                    Process.Start(Environment.ProcessPath!, "--background")?.Dispose();
                }

                // Once per game session: quitting Chest Counter during the game is respected.
                while (IsFortniteRunning() && IsOn)
                {
                    Thread.Sleep(Every);
                }
            }

            Thread.Sleep(Every);
        }

        return 0;
    }

    private static void StartStandby()
    {
        if (Mutex.TryOpenExisting(StandbyName, out var running))
        {
            running.Dispose();
            return;
        }

        Process.Start(Environment.ProcessPath!, Argument)?.Dispose();
    }
}
