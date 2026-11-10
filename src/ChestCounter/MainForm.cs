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
    private ReplayFolderWatcher? _watcher;
    private volatile bool _scanning;
    private string? _error;

    public MainForm(ChestTracker tracker, string demos, string webDataFolder)
    {
        _tracker = tracker;
        _demos = demos;
        _webDataFolder = webDataFolder;
        Text = ChestCounter.Text.AppName;
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = WindowGround();
        _view.DefaultBackgroundColor = BackColor;
        ClientSize = new Size(460, 560);
        MinimumSize = SizeFromClientSize(new Size(380, 530));
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_view);
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
        Close();
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
                    if (_watcher is null)
                    {
                        _watcher = new ReplayFolderWatcher(_demos, Scan, ShowError, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
                        _ = PrepareUpdateAsync();
                    }

                    break;
                case "applyUpdate":
                    _watcher?.Dispose();
                    _updater.ApplyAndRestart();
                    break;
                case "setGamesWithoutReplay" when message.RootElement.GetProperty("value").TryGetInt32(out var games) && games is >= 0 and <= MaxGamesWithoutReplay:
                    _tracker.SetGamesWithoutReplay(games);
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
        _tracker.Scan(_demos, RequestState, cancellation);
        _scanning = false;
        RequestState();
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
        if (IsDisposed || _view.CoreWebView2 is null)
        {
            return;
        }

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
            scanning = _scanning,
            error = _error,
            update = _updater.ReadyVersion,
        }));
    }
}
