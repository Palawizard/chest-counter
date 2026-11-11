namespace ChestCounter;

// One window per Windows session. Opening the app again shows the running window instead,
// unless the second start was a quiet one (--background).
internal sealed class SingleInstance : IDisposable
{
    private const string WindowName = @"Local\Palawi.ChestCounter.Window";
    private const string ShowName = @"Local\Palawi.ChestCounter.Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle show)
    {
        _mutex = mutex;
        _show = show;
    }

    public static bool IsWindowRunning => Mutex.TryOpenExisting(WindowName, out var mutex) && Dispose(mutex);

    public static SingleInstance? TryClaim(bool showRunning)
    {
        var mutex = new Mutex(initiallyOwned: true, WindowName, out var created);
        var show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowName);
        if (created)
        {
            return new SingleInstance(mutex, show);
        }

        if (showRunning)
        {
            show.Set();
        }

        show.Dispose();
        mutex.Dispose();
        return null;
    }

    public void OnShowRequested(Action show) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_show, (_, _) => show(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _wait?.Unregister(null);
        _show.Dispose();
        _mutex.Dispose();
    }

    private static bool Dispose(Mutex mutex)
    {
        mutex.Dispose();
        return true;
    }
}
