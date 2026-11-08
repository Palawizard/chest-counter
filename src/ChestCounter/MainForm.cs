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
    private static readonly Color Enamel = Color.FromArgb(3, 57, 127);
    private static readonly Color Porcelain = Color.FromArgb(243, 245, 248);

    private readonly ChestTracker _tracker;
    private readonly string _demos;
    private readonly string _webDataFolder;
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private ReplayFolderWatcher? _watcher;
    private volatile bool _scanning;
    private string? _error;

    public MainForm(ChestTracker tracker, string demos, string webDataFolder)
    {
        _tracker = tracker;
        _demos = demos;
        _webDataFolder = webDataFolder;
        Text = "Compteur de coffres";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = WindowGround();
        _view.DefaultBackgroundColor = BackColor;
        ClientSize = new Size(460, 490);
        MinimumSize = SizeFromClientSize(new Size(380, 490));
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_view);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _webDataFolder);
            await _view.EnsureCoreWebView2Async(environment);
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or IOException)
        {
            MessageBox.Show("L'affichage n'a pas pu démarrer. Installe le composant Microsoft Edge WebView2, puis relance l'application.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
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
        core.Navigate(Origin + "index.html");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watcher?.Dispose();
        base.OnFormClosed(e);
    }

    // Matches the page's system-theme palette so the window does not flash the other colour while loading.
    private static Color WindowGround() =>
        Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) is int and not 0
            ? Porcelain
            : Enamel;

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
                    _watcher ??= new ReplayFolderWatcher(_demos, Scan, ShowError, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
                    break;
                case "setGamesWithoutReplay" when message.RootElement.GetProperty("value").TryGetInt32(out var games) && games is >= 0 and <= MaxGamesWithoutReplay:
                    _tracker.SetGamesWithoutReplay(games);
                    break;
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _error = "L'enregistrement a échoué. Réessaie dans un instant.";
        }

        PostState();
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
        _error = "La lecture du dossier a échoué. Nouvel essai dans 30 secondes.";
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
            total = summary.Total,
            inProgress = summary.InProgress,
            failed = summary.Failed,
            scanning = _scanning,
            error = _error,
        }));
    }
}
