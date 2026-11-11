using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChestCounter;

// Games without a replay are almost always the oldest ones, so they are estimated from the
// player's earliest counted matches (PerMissingGame, over BasisGames) rather than the overall average.
public sealed record Summary(int GamesAnalyzed, int Chests, int InProgress, int Failed, int GamesWithoutReplay, double BrokenInMatches = 0, double BrokenPerGame = 0, double PerMissingGame = 0, int BasisGames = 0, int Cut = 0, LastMatch? Last = null, int Today = 0, int Best = 0, int TodayGames = 0)
{
    public double Average => GamesAnalyzed == 0 ? 0 : (double)Chests / GamesAnalyzed;

    // Rounded here so the displayed measured and estimated parts add up to the displayed total.
    public double EstimatedChests => Math.Round(PerMissingGame * GamesWithoutReplay, MidpointRounding.AwayFromZero);

    public double EstimatedBroken => Math.Round(BrokenInMatches + BrokenPerGame * GamesWithoutReplay, MidpointRounding.AwayFromZero);

    public double Total => Chests + EstimatedChests + EstimatedBroken;
}

public sealed partial class ChestTracker
{
    public const int BasisSize = 20;

    private static readonly DateTime SeasonStart = new(2026, 11, 1);

    private readonly object _gate = new();
    private readonly string _stateFile;
    private readonly Func<string, ReplayOutcome> _analyze;
    private readonly State _state;
    private readonly Dictionary<string, MatchRecord> _matches;
    private readonly HashSet<string> _failedThisRun = [];
    private readonly HashSet<string> _loggedChestClasses = [];
    private readonly HashSet<string> _needReanalysis;
    private int _inProgress;
    private int _cut;

    public ChestTracker(string stateFile, Func<string, ReplayOutcome> analyze)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(stateFile))!);
        _stateFile = stateFile;
        _analyze = analyze;
        _state = Load();
        if (_state.Matches.Any(m => m.Method < MatchRecord.CurrentMethod))
        {
            if (File.Exists(stateFile) && !File.Exists(BeforeMigrationFile))
            {
                File.Copy(stateFile, BeforeMigrationFile);
            }

            _state.Notice ??= new UpdateNotice { ReadBefore = _state.Matches.Sum(m => m.Chests) };
        }

        _matches = _state.Matches.ToDictionary(m => m.Id);
        _needReanalysis = _state.Matches.Where(m => m.Method < MatchRecord.CurrentMethod).Select(m => m.FileName).ToHashSet();
    }

    // True when the saved results were missing or unreadable and came back from the backup copy.
    public bool RestoredFromBackup { get; private set; }

    private string BackupFile => Path.ChangeExtension(_stateFile, ".backup.json");

    private string BeforeMigrationFile => Path.ChangeExtension(_stateFile, ".2.2.json");

    public Summary Current
    {
        get
        {
            lock (_gate)
            {
                var (brokenInMatches, brokenPerGame) = BrokenEstimates();
                var basis = Earliest(_matches.Values);
                var perMissingGame = basis.Count == 0 ? 0 : basis.Average(m => m.Chests);
                return new Summary(_matches.Count, _matches.Values.Sum(m => m.Chests), _inProgress, _failedThisRun.Count, _state.GamesWithoutReplay, brokenInMatches, brokenPerGame, perMissingGame, basis.Count, _cut, MatchHistory.Last(_matches.Values), MatchHistory.On(_matches.Values, DateTime.Now), MatchHistory.Best(_matches.Values), MatchHistory.GamesOn(_matches.Values, DateTime.Now));
            }
        }
    }

    // The one-time message after the recount, until the player closes it.
    public UpdateNotice? Notice
    {
        get
        {
            lock (_gate)
            {
                return _state.Notice is { Ready: true, Dismissed: false } notice && (notice.ReadBefore != notice.ReadAfter || notice.Moved > 0) ? notice : null;
            }
        }
    }

    public bool Notify
    {
        get
        {
            lock (_gate)
            {
                return _state.Notify;
            }
        }
    }

    // A copy: changes go through ChangeSettings so they are saved.
    public AppSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return _state.Settings with { };
            }
        }
    }

    public void ChangeSettings(Func<AppSettings, AppSettings> change)
    {
        lock (_gate)
        {
            _state.Settings = change(_state.Settings);
            Save();
        }
    }

    public IReadOnlyList<MatchLine> Matches()
    {
        lock (_gate)
        {
            return MatchHistory.Lines(_matches.Values);
        }
    }

    public void DismissNotice()
    {
        lock (_gate)
        {
            if (_state.Notice is { } notice)
            {
                notice.Dismissed = true;
                Save();
            }
        }
    }

    public void SetNotify(bool notify)
    {
        lock (_gate)
        {
            _state.Notify = notify;
            Save();
        }
    }

    public void SetGamesWithoutReplay(int games)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(games);
        lock (_gate)
        {
            _state.GamesWithoutReplay = games;
            Save();
        }
    }

    public void Scan(string demosDirectory, Action? progress = null, CancellationToken cancellation = default)
    {
        var inProgress = 0;
        var cut = 0;
        var present = new HashSet<string>();
        foreach (var path in Directory.EnumerateFiles(demosDirectory, "*.replay", SearchOption.TopDirectoryOnly).Order())
        {
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            var fileName = Path.GetFileName(path);
            present.Add(fileName);
            lock (_gate)
            {
                if ((_state.Processed.Contains(fileName) && !_needReanalysis.Contains(fileName)) || _failedThisRun.Contains(fileName))
                {
                    continue;
                }
            }

            var outcome = _analyze(path);
            lock (_gate)
            {
                if (outcome.Status == ReplayStatus.InProgress)
                {
                    inProgress++;
                }
                else if (outcome.Status == ReplayStatus.Cut)
                {
                    cut++;
                }
                else if (outcome.Status == ReplayStatus.Failed)
                {
                    _failedThisRun.Add(fileName);
                    Log($"{fileName} {outcome.Error}");
                }
                else
                {
                    Record(fileName, outcome.Match!);
                    foreach (var chestClass in outcome.NewChestClasses ?? [])
                    {
                        if (_loggedChestClasses.Add(chestClass))
                        {
                            Log($"{fileName} chest class read without a declared type: {chestClass}");
                        }
                    }
                }
            }

            progress?.Invoke();
        }

        lock (_gate)
        {
            _inProgress = inProgress;
            _cut = cut;
            if (present.Count > 0)
            {
                RetireOldMatchesWithoutReplay(present);
                if (_state.Notice is { Ready: false } notice)
                {
                    notice.ReadAfter = _matches.Values.Sum(m => m.Chests);
                    notice.Moved = _state.Retired.Count;
                    notice.Ready = true;
                    Save();
                }
            }
        }

        progress?.Invoke();
    }

    internal static bool IsInSeason(string branch, DateTime playedAt) =>
        playedAt.Date >= SeasonStart || SeasonBranch().IsMatch(branch);

    // A match counted by an older analysis whose replay is gone cannot be corrected: it becomes a
    // match without a replay, estimated like the others. This only follows a full read of a folder
    // that still has replays, so an emptied or moved folder never retires anything.
    private void RetireOldMatchesWithoutReplay(IReadOnlySet<string> present)
    {
        var old = _state.Matches.Where(m => m.Method < MatchRecord.CurrentMethod && !present.Contains(m.FileName)).ToList();
        if (old.Count == 0)
        {
            return;
        }

        foreach (var match in old)
        {
            _state.Matches.Remove(match);
            _matches.Remove(match.Id);
            _state.Processed.Remove(match.FileName);
            _needReanalysis.Remove(match.FileName);
            _state.Retired.Add(match.Id);
        }

        _state.GamesWithoutReplay += old.Count;
        Save();
    }

    private void Record(string fileName, MatchRecord match)
    {
        if (_state.Retired.Remove(match.Id))
        {
            _state.GamesWithoutReplay = Math.Max(0, _state.GamesWithoutReplay - 1);
        }

        if (_matches.TryGetValue(match.Id, out var known))
        {
            if (known.Method < MatchRecord.CurrentMethod)
            {
                _state.Matches[_state.Matches.IndexOf(known)] = match;
                _matches[match.Id] = match;
            }
        }
        else if (IsInSeason(match.Branch, match.PlayedAt))
        {
            _matches.Add(match.Id, match);
            _state.Matches.Add(match);
        }

        _state.Processed.Add(fileName);
        _needReanalysis.Remove(fileName);
        Save();
    }

    private static List<MatchRecord> Earliest(IEnumerable<MatchRecord> matches) =>
        matches.OrderBy(m => m.PlayedAt).Take(BasisSize).ToList();

    // A counted match whose replay is gone still has its opened chests, so it gets the measured
    // broken-per-opened ratio. A match with no replay at all gets the rate of the earliest matches.
    private (double InMatches, double PerGame) BrokenEstimates()
    {
        var known = _matches.Values.Where(m => m.BrokenNearby is not null).ToList();
        if (known.Count == 0)
        {
            return (0, 0);
        }

        var broken = known.Sum(m => m.BrokenNearby!.Value);
        var opened = known.Sum(m => m.Chests);
        var perOpened = opened == 0 ? 0 : broken / opened;
        var withoutData = _matches.Values.Where(m => m.BrokenNearby is null).Sum(m => m.Chests);
        var basis = Earliest(known);
        return (broken + perOpened * withoutData, basis.Sum(m => m.BrokenNearby!.Value) / basis.Count);
    }

    private void Log(string line) =>
        File.AppendAllText(Path.ChangeExtension(_stateFile, ".errors.log"), $"{DateTime.Now:s} {line}{Environment.NewLine}");

    // An unreadable file is set aside, never deleted. Without a backup it is refused as before, so
    // the player's data is left as it was.
    private State Load()
    {
        try
        {
            if (File.Exists(_stateFile))
            {
                return Read(_stateFile);
            }
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            if (!File.Exists(BackupFile))
            {
                throw;
            }

            File.Move(_stateFile, $"{_stateFile}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}");
        }

        if (!File.Exists(BackupFile))
        {
            return new State();
        }

        File.Copy(BackupFile, _stateFile, overwrite: true);
        RestoredFromBackup = true;
        return Read(_stateFile);
    }

    private static State Read(string file) =>
        JsonSerializer.Deserialize<State>(File.ReadAllText(file)) ?? throw new InvalidDataException("empty state file");

    // Written through a temporary file so a crash cannot leave a truncated state behind, then copied
    // as the backup for when the file is deleted or damaged from outside.
    private void Save()
    {
        var temporary = _stateFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_state));
        File.Move(temporary, _stateFile, overwrite: true);
        File.Copy(_stateFile, BackupFile, overwrite: true);
    }

    [GeneratedRegex(@"Release-43\.")]
    private static partial Regex SeasonBranch();

    private sealed class State
    {
        public List<MatchRecord> Matches { get; set; } = [];

        public HashSet<string> Processed { get; set; } = [];

        public int GamesWithoutReplay { get; set; }

        // Matches moved to GamesWithoutReplay; one whose replay comes back is read again and moved back.
        public HashSet<string> Retired { get; set; } = [];

        public UpdateNotice? Notice { get; set; }

        public bool Notify { get; set; } = true;

        public AppSettings Settings { get; set; } = new();
    }
}

// Read chests before the recount, after it, and matches moved to the estimate. Ready once the first
// full scan of 2.3 is done.
public sealed class UpdateNotice
{
    public int ReadBefore { get; set; }

    public int ReadAfter { get; set; }

    public int Moved { get; set; }

    public bool Ready { get; set; }

    public bool Dismissed { get; set; }
}

// Window and in-game options. TrayHintShown: the message saying the window went to the notification area was seen.
public sealed record AppSettings(bool Overlay = false, int? OverlayX = null, int? OverlayY = null, bool Obs = false, bool TrayHintShown = false);
