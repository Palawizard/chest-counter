using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChestCounter;

public sealed record Summary(int GamesAnalyzed, int Chests, int InProgress, int Failed, int GamesWithoutReplay)
{
    public double Average => GamesAnalyzed == 0 ? 0 : (double)Chests / GamesAnalyzed;

    // Rounded here so the displayed measured and estimated parts add up to the displayed total.
    public double EstimatedChests => Math.Round(Average * GamesWithoutReplay, MidpointRounding.AwayFromZero);

    public double Total => Chests + EstimatedChests;
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
    }

    public Summary Current
    {
        get
        {
            lock (_gate)
            {
                return new Summary(_matches.Count, _matches.Values.Sum(m => m.Chests), _inProgress, _failedThisRun.Count, _state.GamesWithoutReplay);
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
                if (_state.Processed.Contains(fileName) || _failedThisRun.Contains(fileName))
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
        if (IsInSeason(match.Branch, match.PlayedAt) && _matches.TryAdd(match.Id, match))
        {
            _state.Matches.Add(match);
        }

        _state.Processed.Add(fileName);
        Save();
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
