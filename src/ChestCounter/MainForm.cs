using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ChestCounter;

internal sealed class MainForm : Form
{
    private const string Host = "chest-counter.example";
    private const string Origin = $"https://{Host}/";
    private const int MaxGamesWithoutReplay = 100000;
    private static readonly Color DarkPage = Color.FromArgb(36, 22, 10);
    private static readonly Color LightPage = Color.FromArgb(242, 212, 148);

    private readonly ChestTracker _tracker;
    private readonly string _demos;
    private readonly string _webDataFolder;
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly AppUpdater _updater = new();
    private readonly InGame _inGame;
    private readonly TrayIcon _tray;
    private bool _startHidden;
    private bool _quitting;
    private ReplayFolderWatcher? _watcher;
    private volatile bool _scanning;
    private string? _error;
    private int _completedScans;

    public MainForm(ChestTracker tracker, string demos, string webDataFolder, bool startHidden)
    {
        _tracker = tracker;
        _demos = demos;
        _webDataFolder = webDataFolder;
        _startHidden = startHidden;
        Text = ChestCounter.Text.AppName;
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
        _inGame = new InGame(this, tracker, OverlayFigures, ObsState);
        _tray = new TrayIcon(Icon, ShowFromTray, ToggleOverlay, Quit);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = WindowGround();
        _view.DefaultBackgroundColor = BackColor;
        ClientSize = new Size(460, 700);
        MinimumSize = SizeFromClientSize(new Size(380, 640));
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_view);
    }

    // Started with Windows or with Fortnite: counting runs without showing the window.
    protected override void SetVisibleCore(bool value)
    {
        if (_startHidden && !IsHandleCreated)
        {
            CreateHandle();
            value = false;
        }

        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _watcher ??= new ReplayFolderWatcher(_demos, Scan, ShowError, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
        _ = PrepareUpdateAsync();
        _inGame.Apply();
        _tray.OverlayOn = _tracker.Settings.Overlay;
    }

    public void ShowFromTray()
    {
        if (InvokeRequired)
        {
            BeginInvoke(ShowFromTray);
            return;
        }

        _startHidden = false;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    // The cross hides the window: counting, the overlay and the OBS page go on until Quit.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_quitting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_tracker.Settings.TrayHintShown)
            {
                _tracker.ChangeSettings(settings => settings with { TrayHintShown = true });
                _tray.Announce(ChestCounter.Text.AppName, ChestCounter.Text.Pick("Chest Counter continue de compter ici. Clic droit sur l'icône pour quitter.", "Chest Counter keeps counting here. Right-click the icon to quit."));
            }

            return;
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotKey)
        {
            _inGame.OnHotKey(m.WParam.ToInt32());
            PostState();
        }

        base.WndProc(ref m);
    }

    private void ToggleOverlay()
    {
        _tracker.ChangeSettings(settings => settings with { Overlay = !settings.Overlay });
        PostState();
    }

    private (string Total, string Last) OverlayFigures()
    {
        var summary = _tracker.Current;
        var culture = System.Globalization.CultureInfo.GetCultureInfo(ChestCounter.Text.French ? "fr-FR" : "en-US");
        var last = summary.Last is { } match ? ChestCounter.Text.Pick($"Partie d'avant\u00a0: {match.Total.ToString("N0", culture)}", $"Last match: {match.Total.ToString("N0", culture)}") : "";
        return (Math.Round(summary.Total).ToString("N0", culture), last);
    }

    private string ObsState()
    {
        var summary = _tracker.Current;
        return JsonSerializer.Serialize(new { lang = ChestCounter.Text.Language, total = summary.Total, last = summary.Last?.Total });
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            // No environment options: every process sharing this data folder, 1.x included,
            // must create WebView2 with identical options or the second one fails.
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _webDataFolder);
            await _view.EnsureCoreWebView2Async(environment);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowStartupError(ChestCounter.Text.Pick("L'affichage n'a pas pu démarrer. Installe le composant Microsoft Edge WebView2, puis relance l'application.", "The window could not start. Install Microsoft Edge WebView2, then open the app again."));
            return;
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            LogStartupError(exception);
            ShowStartupError(ChestCounter.Text.Pick("L'affichage n'a pas pu démarrer. Ferme les autres fenêtres de Chest Counter, puis relance l'application.", "The window could not start. Close any other Chest Counter window, then open the app again."));
            return;
        }

        var core = _view.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Deny);
        core.NavigationStarting += (_, args) => args.Cancel = !args.Uri.StartsWith(Origin, StringComparison.Ordinal);
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.WebMessageReceived += OnPageMessage;
        core.Navigate(Origin + "index.html?lang=" + ChestCounter.Text.Language);
    }

    private void ShowStartupError(string message)
    {
        MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        Quit();
    }

    private void LogStartupError(Exception exception)
    {
        try
        {
            var log = Path.Combine(Path.GetDirectoryName(_webDataFolder)!, "display.errors.log");
            File.AppendAllText(log, $"{DateTime.Now:s} {exception.GetType().Name} 0x{exception.HResult:X8} {exception.Message}{Environment.NewLine}");
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
        {
            // The message box still tells the player what to do.
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watcher?.Dispose();
        _inGame.Dispose();
        _tray.Dispose();
        base.OnFormClosed(e);
    }

    // Matches the page's system-theme palette so the window does not flash the other colour while loading.
    private static Color WindowGround() =>
        Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) is int and not 0
            ? LightPage
            : DarkPage;

    private void OnPageMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!e.Source.StartsWith(Origin, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            switch (message.RootElement.GetProperty("type").GetString())
            {
                case "ready":
                    break;
                case "applyUpdate":
                    _quitting = true;
                    _watcher?.Dispose();
                    _updater.ApplyAndRestart();
                    break;
                case "setGamesWithoutReplay" when message.RootElement.GetProperty("value").TryGetInt32(out var games) && games is >= 0 and <= MaxGamesWithoutReplay:
                    _tracker.SetGamesWithoutReplay(games);
                    break;
                case "dismissNotice":
                    _tracker.DismissNotice();
                    break;
                case "setNotify":
                    _tracker.SetNotify(message.RootElement.GetProperty("value").GetBoolean());
                    break;
                case "matches":
                    PostMatches();
                    return;
                case "setOverlay":
                    var overlay = message.RootElement.GetProperty("value").GetBoolean();
                    _tracker.ChangeSettings(settings => settings with { Overlay = overlay });
                    break;
                case "setObs":
                    var obs = message.RootElement.GetProperty("value").GetBoolean();
                    _tracker.ChangeSettings(settings => settings with { Obs = obs });
                    break;
                case "setStartWithFortnite":
                    StartWithFortnite.Set(message.RootElement.GetProperty("value").GetBoolean());
                    break;
                case "moveOverlay":
                    _inGame.ToggleMoving();
                    break;
                case "copyObs" when _inGame.ObsAddress is string address:
                    Clipboard.SetText(address);
                    break;
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _error = "save";
        }

        PostState();
    }

    private async Task PrepareUpdateAsync()
    {
        if (await _updater.PrepareAsync())
        {
            PostState();
        }
    }

    private void Scan(CancellationToken cancellation)
    {
        _scanning = true;
        _error = null;
        var before = _tracker.Current.GamesAnalyzed;
        _tracker.Scan(_demos, RequestState, cancellation);
        _scanning = false;
        var summary = _tracker.Current;
        // The first scan reads what was played while the app was closed: only later matches are announced.
        if (_completedScans++ > 0 && summary.GamesAnalyzed > before && summary.Last is { } last && _tracker.Notify && IsHandleCreated)
        {
            BeginInvoke(() => Announce(last, summary));
        }

        RequestState();
    }

    private void Announce(LastMatch last, Summary summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var culture = System.Globalization.CultureInfo.GetCultureInfo(ChestCounter.Text.French ? "fr-FR" : "en-US");
        _tray.Announce(
            ChestCounter.Text.Pick($"+{last.Total.ToString("N0", culture)} {(last.Total > 1 ? "coffres" : "coffre")}", $"+{last.Total.ToString("N0", culture)} {(last.Total == 1 ? "chest" : "chests")}"),
            ChestCounter.Text.Pick($"Total estimé : {summary.Total.ToString("N0", culture)}", $"Estimated total: {summary.Total.ToString("N0", culture)}"));
    }

    private void PostMatches()
    {
        if (IsDisposed || _view.CoreWebView2 is null)
        {
            return;
        }

        _view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            matches = _tracker.Matches().Select(m => new { playedAt = m.PlayedAt, chests = m.Chests, broken = m.Broken, kinds = m.Kinds }),
        }));
    }

    private void ShowError(Exception exception)
    {
        _scanning = false;
        _error = "read";
        RequestState();
    }

    private void RequestState()
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(PostState);
        }
    }

    private void PostState()
    {
        if (IsDisposed)
        {
            return;
        }

        _inGame.Apply();
        _tray.OverlayOn = _tracker.Settings.Overlay;
        if (_view.CoreWebView2 is null)
        {
            return;
        }

        var settings = _tracker.Settings;

        var summary = _tracker.Current;
        _view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            gamesAnalyzed = summary.GamesAnalyzed,
            chests = summary.Chests,
            average = summary.Average,
            gamesWithoutReplay = summary.GamesWithoutReplay,
            estimated = summary.EstimatedChests,
            broken = summary.EstimatedBroken,
            brokenPerGame = summary.BrokenPerGame,
            basisGames = summary.BasisGames,
            total = summary.Total,
            inProgress = summary.InProgress,
            failed = summary.Failed,
            cut = summary.Cut,
            restored = _tracker.RestoredFromBackup,
            last = summary.Last is { } last ? new { chests = last.Chests, broken = last.Broken, endedAt = last.EndedAt } : null,
            today = summary.Today,
            todayGames = summary.TodayGames,
            best = summary.Best,
            notice = _tracker.Notice is { } notice ? new { before = notice.ReadBefore, after = notice.ReadAfter, moved = notice.Moved } : null,
            scanning = _scanning,
            error = _error,
            update = _updater.ReadyVersion,
            settings = new
            {
                notify = _tracker.Notify,
                overlay = settings.Overlay,
                hotKeysTaken = _inGame.OverlayHotKeysTaken,
                overlayMoving = _inGame.OverlayMoving,
                obs = settings.Obs,
                obsAddress = _inGame.ObsAddress,
                obsFailed = settings.Obs && _inGame.ObsFailed,
                startWithFortnite = StartWithFortnite.IsOn,
            },
        }));
    }
}
