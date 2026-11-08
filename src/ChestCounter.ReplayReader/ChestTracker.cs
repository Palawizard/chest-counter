using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChestCounter;

public sealed record Summary(int GamesAnalyzed, int Chests, int InProgress, int Failed, int GamesWithoutReplay, double BrokenPerGame = 0)
{
    public double Average => GamesAnalyzed == 0 ? 0 : (double)Chests / GamesAnalyzed;

    // Rounded here so the displayed measured and estimated parts add up to the displayed total.
    public double EstimatedChests => Math.Round(Average * GamesWithoutReplay, MidpointRounding.AwayFromZero);

    public double EstimatedBroken => Math.Round(BrokenPerGame * (GamesAnalyzed + GamesWithoutReplay), MidpointRounding.AwayFromZero);

    public double Total => Chests + EstimatedChests + EstimatedBroken;
}

public sealed partial class ChestTracker
{
    private static readonly DateTime SeasonStart = new(2026, 11, 1);

    private readonly object _gate = new();
    private readonly string _stateFile;
    private readonly Func<string, ReplayOutcome> _analyze;
    private readonly State _state;
    private readonly Dictionary<string, MatchRecord> _matches;
    private readonly HashSet<string> _failedThisRun = [];
    private readonly HashSet<string> _missingBrokenData;
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
        _missingBrokenData = _state.Matches.Where(m => m.DestroyedUnopened is null).Select(m => m.FileName).ToHashSet();
    }

    public Summary Current
    {
        get
        {
            lock (_gate)
            {
                return new Summary(_matches.Count, _matches.Values.Sum(m => m.Chests), _inProgress, _failedThisRun.Count, _state.GamesWithoutReplay, BrokenPerGame());
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
        foreach (var path in Directory.EnumerateFiles(demosDirectory, "*.replay", SearchOption.TopDirectoryOnly).Order())
        {
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            var fileName = Path.GetFileName(path);
            lock (_gate)
            {
                if ((_state.Processed.Contains(fileName) && !_missingBrokenData.Contains(fileName)) || _failedThisRun.Contains(fileName))
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
                    File.AppendAllText(Path.ChangeExtension(_stateFile, ".errors.log"), $"{DateTime.Now:s} {fileName} {outcome.Error}{Environment.NewLine}");
                }
                else
                {
                    Record(fileName, outcome.Match!);
                }
            }

            progress?.Invoke();
        }

        lock (_gate)
        {
            _inProgress = inProgress;
        }

        progress?.Invoke();
    }

    internal static bool IsInSeason(string branch, DateTime playedAt) =>
        playedAt.Date >= SeasonStart || SeasonBranch().IsMatch(branch);

    private void Record(string fileName, MatchRecord match)
    {
        if (_matches.TryGetValue(match.Id, out var known))
        {
            if (known.DestroyedUnopened is null)
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
        _missingBrokenData.Remove(fileName);
        Save();
    }

    // Replays do not say who broke a chest, so the player's share of known openings stands in for it.
    private double BrokenPerGame()
    {
        var known = _matches.Values.Where(m => m.DestroyedUnopened is not null).ToList();
        var own = known.Sum(m => m.Chests);
        var openings = own + known.Sum(m => m.OtherOpenings ?? 0);
        return openings == 0 ? 0 : (double)own / openings * known.Sum(m => m.DestroyedUnopened!.Value) / known.Count;
    }

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
    }
}
