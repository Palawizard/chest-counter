using System.Globalization;

namespace ChestCounter;

internal sealed class MainForm : Form
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ChestTracker _tracker;
    private readonly string _demos;
    private readonly Label _games = NewValue();
    private readonly Label _chests = NewValue();
    private readonly Label _average = NewValue();
    private readonly Label _measured = NewValue();
    private readonly Label _estimated = NewValue();
    private readonly Label _total = NewValue();
    private readonly Label _status = new() { AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
    private readonly NumericUpDown _gamesPlayed = new() { Minimum = 0, Maximum = 100000, Width = 90, AccessibleName = "Parties jouées depuis le 1er novembre" };
    private ReplayFolderWatcher? _watcher;
    private volatile bool _scanning;
    private string? _error;

    public MainForm(ChestTracker tracker, string demos)
    {
        _tracker = tracker;
        _demos = demos;
        Text = "Compteur de coffres";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        AddRow(layout, "Parties analysées", _games);
        AddRow(layout, "Coffres comptés", _chests);
        AddRow(layout, "Moyenne par partie", _average);
        AddRow(layout, "Parties jouées depuis le 1er novembre", _gamesPlayed);
        AddRow(layout, "Part mesurée", _measured);
        AddRow(layout, "Part estimée", _estimated);
        AddRow(layout, "Total estimé", _total);
        layout.Controls.Add(_status);
        layout.SetColumnSpan(_status, 2);
        Controls.Add(layout);

        _gamesPlayed.Value = tracker.Current.GamesPlayed ?? 0;
        _gamesPlayed.ValueChanged += (_, _) =>
        {
            _tracker.SetGamesPlayed(_gamesPlayed.Value == 0 ? null : (int)_gamesPlayed.Value);
            Refresh(_tracker.Current);
        };
        Refresh(tracker.Current);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _watcher = new ReplayFolderWatcher(_demos, Scan, ShowError, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watcher?.Dispose();
        base.OnFormClosed(e);
    }

    private static Label NewValue() => new() { AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold) };

    private static void AddRow(TableLayoutPanel layout, string caption, Control value)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 4, 24, 4) });
        value.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(value);
    }

    private void Scan()
    {
        _scanning = true;
        _error = null;
        _tracker.Scan(_demos, PostRefresh);
        _scanning = false;
        PostRefresh();
    }

    private void ShowError(Exception e)
    {
        _scanning = false;
        _error = "La lecture du dossier a échoué. Nouvel essai dans 30 secondes.";
        PostRefresh();
    }

    private void PostRefresh()
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(() => Refresh(_tracker.Current));
        }
    }

    private void Refresh(Summary summary)
    {
        _games.Text = summary.GamesAnalyzed.ToString("N0", French);
        _chests.Text = summary.Chests.ToString("N0", French);
        _average.Text = summary.Average.ToString("N1", French);
        _measured.Text = $"{summary.Chests.ToString("N0", French)} coffres sur {summary.GamesAnalyzed.ToString("N0", French)} parties";

        if (summary.GamesPlayed is null)
        {
            _estimated.Text = "Indique tes parties jouées";
            _total.Text = "-";
        }
        else if (summary.GamesPlayed < summary.GamesAnalyzed)
        {
            _estimated.Text = $"Indique au moins {summary.GamesAnalyzed.ToString("N0", French)} parties";
            _total.Text = "-";
        }
        else
        {
            _estimated.Text = $"{summary.EstimatedChests.ToString("N0", French)} coffres sur {summary.GamesWithoutReplay.ToString("N0", French)} parties sans replay";
            _total.Text = $"{summary.Total.ToString("N0", French)} coffres";
        }

        _status.Text = _error ?? string.Join(" ", new[]
        {
            _scanning ? "Analyse en cours." : "À jour.",
            summary.InProgress > 0 ? "Une partie en cours sera comptée à la fin." : null,
            summary.Failed > 0 ? $"Replays illisibles : {summary.Failed}." : null,
        }.Where(part => part is not null));
    }
}
