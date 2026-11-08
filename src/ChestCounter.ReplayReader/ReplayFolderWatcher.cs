namespace ChestCounter;

public sealed class ReplayFolderWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _timer;
    private readonly Action<CancellationToken> _scan;
    private readonly CancellationTokenSource _stop = new();
    private readonly Action<Exception> _onError;
    private readonly TimeSpan _settleDelay;
    private readonly TimeSpan _period;
    private readonly object _running = new();

    public ReplayFolderWatcher(string directory, Action<CancellationToken> scan, Action<Exception> onError, TimeSpan settleDelay, TimeSpan period)
    {
        _scan = scan;
        _onError = onError;
        _settleDelay = settleDelay;
        _period = period;
        _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, period);
        _watcher = new FileSystemWatcher(directory, "*.replay")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _watcher.Dispose();
        _timer.Dispose();
        lock (_running)
        {
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => _timer.Change(_settleDelay, _period);

    // The periodic tick stays on because file change notifications can be delayed or dropped.
    private void Tick()
    {
        if (_stop.IsCancellationRequested || !Monitor.TryEnter(_running))
        {
            return;
        }

        try
        {
            _scan(_stop.Token);
        }
        catch (Exception e)
        {
            _onError(e);
        }
        finally
        {
            Monitor.Exit(_running);
        }
    }
}
