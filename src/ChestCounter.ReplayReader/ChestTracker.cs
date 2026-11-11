using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChestCounter;

// Games without a replay are almost always the oldest ones, so they are estimated from the
// player's earliest counted matches (PerMissingGame, over BasisGames) rather than the overall average.
public sealed record Summary(int GamesAnalyzed, int Chests, int InProgress, int Failed, int GamesWithoutReplay, double BrokenInMatches = 0, double BrokenPerGame = 0, double PerMissingGame = 0, int BasisGames = 0)
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

    public ChestTracker(string stateFile, Func<string, ReplayOutcome> analyze)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(stateFile))!);
        _stateFile = stateFile;
        _analyze = analyze;
        _state = File.Exists(stateFile)
            ? JsonSerializer.Deserialize<State>(File.ReadAllText(stateFile)) ?? throw new InvalidDataException("empty state file")
            : new State();
        _matches = _state.Matches.ToDictionary(m => m.Id);
        _needReanalysis = _state.Matches.Where(m => m.Method < MatchRecord.CurrentMethod).Select(m => m.FileName).ToHashSet();
    }

    public Summary Current
    {
        get
        {
            lock (_gate)
            {
                var (brokenInMatches, brokenPerGame) = BrokenEstimates();
                var basis = Earliest(_matches.Values);
                var perMissingGame = basis.Count == 0 ? 0 : basis.Average(m => m.Chests);
                return new Summary(_matches.Count, _matches.Values.Sum(m => m.Chests), _inProgress, _failedThisRun.Count, _state.GamesWithoutReplay, brokenInMatches, brokenPerGame, perMissingGame, basis.Count);
            }
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
            if (present.Count > 0)
            {
                RetireOldMatchesWithoutReplay(present);
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

    // Written through a temporary file so a crash cannot leave a truncated state behind.
    private void Save()
    {
        var temporary = _stateFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_state));
        File.Move(temporary, _stateFile, overwrite: true);
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
    }
}
