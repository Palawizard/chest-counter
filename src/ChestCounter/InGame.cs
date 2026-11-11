namespace ChestCounter;

// The overlay and the OBS page: shown only while Fortnite runs, driven by the window's settings.
internal sealed class InGame : IDisposable
{
    public const int ToggleHotKey = 1;
    public const int MoveHotKey = 2;
    private const uint KeyO = 0x4F;
    private const uint KeyL = 0x4C;

    private readonly Form _host;
    private readonly ChestTracker _tracker;
    private readonly Func<(string Total, string Last)> _figures;
    private readonly Func<string> _obsState;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 3000 };
    private OverlayWindow? _overlay;
    private ObsServer? _obs;
    private bool _hotKeys;
    private bool _fortnite;
    private bool _hiddenByKey;

    public bool OverlayVisible => _overlay?.Visible == true;

    public InGame(Form host, ChestTracker tracker, Func<(string Total, string Last)> figures, Func<string> obsState)
    {
        _host = host;
        _tracker = tracker;
        _figures = figures;
        _obsState = obsState;
        _poll.Tick += (_, _) => Poll();
    }

    public string? ObsAddress => _obs?.Address;

    public bool ObsFailed { get; private set; }

    public bool OverlayHotKeysTaken { get; private set; }

    // Called on the UI thread whenever a setting or a figure changes.
    public void Apply()
    {
        var settings = _tracker.Settings;
        SetHotKeys(settings.Overlay);
        var polling = _poll.Enabled;
        _poll.Enabled = settings.Overlay;
        if (settings.Overlay && !polling)
        {
            Poll();
        }

        if (settings.Obs && _obs is null)
        {
            try
            {
                _obs = ObsServer.Start(Path.Combine(AppContext.BaseDirectory, "wwwroot"), _obsState);
                ObsFailed = false;
            }
            catch (IOException)
            {
                ObsFailed = true;
            }
        }
        else if (!settings.Obs && _obs is not null)
        {
            _obs.Dispose();
            _obs = null;
        }

        if (!settings.Overlay)
        {
            _hiddenByKey = false;
            if (_overlay is not null)
            {
                _overlay.Moving = false;
            }
        }

        UpdateOverlay(settings);
    }

    public void OnHotKey(int id)
    {
        if (id == ToggleHotKey)
        {
            _hiddenByKey = !_hiddenByKey;
        }
        else if (id == MoveHotKey && _overlay is not null)
        {
            _overlay.Moving = !_overlay.Moving;
        }
        else if (id == MoveHotKey)
        {
            Overlay().Moving = true;
        }

        UpdateOverlay(_tracker.Settings);
    }

    public void Dispose()
    {
        SetHotKeys(false);
        _poll.Dispose();
        _overlay?.Dispose();
        _obs?.Dispose();
    }

    private void Poll()
    {
        _fortnite = StartWithFortnite.IsFortniteRunning();
        UpdateOverlay(_tracker.Settings);
    }

    private void UpdateOverlay(AppSettings settings)
    {
        var moving = _overlay?.Moving == true;
        var visible = settings.Overlay && (moving || (_fortnite && !_hiddenByKey));
        if (!visible)
        {
            _overlay?.Hide();
            return;
        }

        var overlay = Overlay();
        var (total, last) = _figures();
        overlay.Text = ChestCounter.Text.Pick("Total estimé", "Estimated total");
        overlay.Show(total, last);
        if (!overlay.Visible)
        {
            var saved = settings.OverlayX is int x && settings.OverlayY is int y ? new Point(x, y) : (Point?)null;
            overlay.Location = OverlayPlacement.Place(saved, overlay.Size, [.. Screen.AllScreens.Select(screen => screen.WorkingArea)], Screen.PrimaryScreen!.WorkingArea);
            overlay.Show();
        }
    }

    private OverlayWindow Overlay()
    {
        if (_overlay is null)
        {
            _overlay = new OverlayWindow();
            _overlay.Moved += spot => _tracker.ChangeSettings(settings => settings with { OverlayX = spot.X, OverlayY = spot.Y });
        }

        return _overlay;
    }

    // Another app may already own these keys: the overlay still works, only the keys do not.
    private void SetHotKeys(bool on)
    {
        if (on == _hotKeys || !_host.IsHandleCreated)
        {
            return;
        }

        if (on)
        {
            var modifiers = NativeMethods.ModControl | NativeMethods.ModShift | NativeMethods.ModNoRepeat;
            var toggle = NativeMethods.RegisterHotKey(_host.Handle, ToggleHotKey, modifiers, KeyO);
            var move = NativeMethods.RegisterHotKey(_host.Handle, MoveHotKey, modifiers, KeyL);
            OverlayHotKeysTaken = !toggle || !move;
        }
        else
        {
            NativeMethods.UnregisterHotKey(_host.Handle, ToggleHotKey);
            NativeMethods.UnregisterHotKey(_host.Handle, MoveHotKey);
            OverlayHotKeysTaken = false;
        }

        _hotKeys = on;
    }
}
