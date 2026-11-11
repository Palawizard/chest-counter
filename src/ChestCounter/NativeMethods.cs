using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChestCounter;

internal static class NativeMethods
{
    public const int WmHotKey = 0x0312;
    public const int WsExTransparent = 0x20;
    public const int WsExToolWindow = 0x80;
    public const int WsExLayered = 0x80000;
    public const int WsExNoActivate = 0x8000000;
    public const uint ModControl = 0x2;
    public const uint ModShift = 0x4;
    public const uint ModNoRepeat = 0x4000;
    public const uint EventSystemForeground = 0x3;
    private const int GwlExStyle = -20;
    private const int WmNcLeftButtonDown = 0xA1;
    private const int HtCaption = 2;
    private const int ProcessPowerThrottling = 4;
    private const uint ThrottleExecutionSpeed = 1;
    private const uint SwpNoSizeNoMoveNoActivate = 0x13;
    private static readonly IntPtr TopMost = new(-1);

    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr window, int id);

    // Click-through on or off for the overlay, without recreating its window.
    public static void SetClickThrough(IntPtr window, bool clickThrough)
    {
        var style = GetWindowLongPtrW(window, GwlExStyle).ToInt64();
        style = clickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLongPtrW(window, GwlExStyle, new IntPtr(style));
    }

    // A full screen game is topmost too and goes in front of the overlay each time it gets the focus.
    public static void KeepOnTop(IntPtr window) => SetWindowPos(window, TopMost, 0, 0, 0, 0, SwpNoSizeNoMoveNoActivate);

    // Out of context: the callback runs on the calling thread's message loop, nothing is injected into the game.
    public static IntPtr WatchForeground(WinEventProc callback) => SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, callback, 0, 0, 0);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hook);

    // Lets a borderless window be dragged like a title bar.
    public static void StartDrag(IntPtr window)
    {
        ReleaseCapture();
        SendMessageW(window, WmNcLeftButtonDown, HtCaption, 0);
    }

    // Low priority and Windows' efficiency mode (EcoQoS), for work that must not cost the game anything.
    public static void RunInBackground()
    {
        using var self = Process.GetCurrentProcess();
        try
        {
            self.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Not allowed: the work still runs at normal priority.
        }

        var state = new PowerThrottlingState { Version = 1, ControlMask = ThrottleExecutionSpeed, StateMask = ThrottleExecutionSpeed };
        SetProcessInformation(self.Handle, ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr window, int message, int wParam, int lParam);
}
