namespace ChestCounter.Tests;

public sealed class ChestTrackerTests : IDisposable
{
    private const string Season = "++Fortnite+Release-43.00";
    private const string Earlier = "++Fortnite+Release-42.10";
    private static readonly DateTime InSeason = new(2026, 11, 8);
    private static readonly DateTime BeforeSeason = new(2026, 10, 20);

    private readonly string _root = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;
    private readonly Dictionary<string, ReplayOutcome> _outcomes = [];
    private readonly List<string> _analyzed = [];

    public ChestTrackerTests() => Directory.CreateDirectory(Demos);

    private string Demos => Path.Combine(_root, "demos");

    private string StateFile => Path.Combine(_root, "state", "state.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Scan_counts_a_replay_once_across_rescans()
    {
        var tracker = NewTracker();
        AddReplay("a.replay", "id-a", 10);

        tracker.Scan(Demos);
        tracker.Scan(Demos);

        Assert.Equal(10, tracker.Current.Chests);
        Assert.Single(_analyzed);
    }

    [Fact]
    public void Restart_does_not_reanalyze_or_recount()
    {
        AddReplay("a.replay", "id-a", 10);
        NewTracker().Scan(Demos);
        _analyzed.Clear();

        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Empty(_analyzed);
        Assert.Equal(1, restarted.Current.GamesAnalyzed);
        Assert.Equal(10, restarted.Current.Chests);
    }

    [Fact]
    public void Same_match_under_a_new_file_name_is_not_counted_twice()
    {
        var tracker = NewTracker();
        AddReplay("UnsavedReplay.replay", "id-a", 10);
        AddReplay("MySavedGame.replay", "id-a", 10);

        tracker.Scan(Demos);

        Assert.Equal(1, tracker.Current.GamesAnalyzed);
        Assert.Equal(10, tracker.Current.Chests);
    }

    [Fact]
    public void Counted_match_survives_deletion_of_its_replay()
    {
        AddReplay("a.replay", "id-a", 10);
        NewTracker().Scan(Demos);
        File.Delete(Path.Combine(Demos, "a.replay"));

        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Equal(10, restarted.Current.Chests);
    }

    [Fact]
    public void Replay_in_progress_is_ignored_then_counted_once_finished()
    {
        var tracker = NewTracker();
        AddReplay("a.replay", "id-a", 10);
        _outcomes["a.replay"] = new ReplayOutcome(ReplayStatus.InProgress);

        tracker.Scan(Demos);
        Assert.Equal(0, tracker.Current.Chests);
        Assert.Equal(1, tracker.Current.InProgress);

        AddReplay("a.replay", "id-a", 10);
        tracker.Scan(Demos);
        tracker.Scan(Demos);

        Assert.Equal(10, tracker.Current.Chests);
        Assert.Equal(0, tracker.Current.InProgress);
    }

    [Theory]
    [InlineData(Earlier, 2026, 10, 20, false)]
    [InlineData(Season, 2026, 10, 20, true)]
    [InlineData(Earlier, 2026, 11, 1, true)]
    [InlineData(Season, 2026, 11, 8, true)]
    public void Season_requires_the_date_or_the_branch(string branch, int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, ChestTracker.IsInSeason(branch, new DateTime(year, month, day)));
    }

    [Fact]
    public void Out_of_season_replay_is_excluded_and_not_reanalyzed()
    {
        var tracker = NewTracker();
        AddReplay("old.replay", "id-old", 50, Earlier, BeforeSeason);

        tracker.Scan(Demos);
        tracker.Scan(Demos);

        Assert.Equal(0, tracker.Current.GamesAnalyzed);
        Assert.Single(_analyzed);
    }

    [Fact]
    public void Failed_replay_is_reported_and_retried_only_after_restart()
    {
        var tracker = NewTracker();
        File.WriteAllBytes(Path.Combine(Demos, "bad.replay"), []);
        _outcomes["bad.replay"] = new ReplayOutcome(ReplayStatus.Failed, Error: "boom");

        tracker.Scan(Demos);
        tracker.Scan(Demos);

        Assert.Equal(1, tracker.Current.Failed);
        Assert.Single(_analyzed);
        NewTracker().Scan(Demos);
        Assert.Equal(2, _analyzed.Count);
    }

    [Theory]
    [InlineData(4, 100, 6, 2, 25.0, 50.0, 150.0)]
    [InlineData(4, 15, 10, 6, 3.75, 23.0, 38.0)]
    [InlineData(4, 100, 4, 0, 25.0, 0.0, 100.0)]
    [InlineData(4, 100, 2, 0, 25.0, 0.0, 100.0)]
    [InlineData(0, 0, 10, 10, 0.0, 0.0, 0.0)]
    public void Estimate_extrapolates_the_average_to_games_without_replay(int analyzed, int chests, int played, int missing, double average, double estimated, double total)
    {
        var summary = new Summary(analyzed, chests, 0, 0, played);

        Assert.Equal(average, summary.Average);
        Assert.Equal(missing, summary.GamesWithoutReplay);
        Assert.Equal(estimated, summary.EstimatedChests);
        Assert.Equal(total, summary.Total);
    }

    [Fact]
    public void Games_played_survives_a_restart()
    {
        NewTracker().SetGamesPlayed(120);

        Assert.Equal(120, NewTracker().Current.GamesPlayed);
    }

    [Fact]
    public void Corrupted_state_is_refused_and_left_untouched()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        File.WriteAllText(StateFile, "{ broken");

        Assert.ThrowsAny<Exception>(NewTracker);
        Assert.Equal("{ broken", File.ReadAllText(StateFile));
    }

    private ChestTracker NewTracker() => new(StateFile, path =>
    {
        var name = Path.GetFileName(path);
        _analyzed.Add(name);
        return _outcomes[name];
    });

    private void AddReplay(string fileName, string id, int chests, string branch = Season, DateTime? playedAt = null)
    {
        File.WriteAllBytes(Path.Combine(Demos, fileName), []);
        _outcomes[fileName] = new ReplayOutcome(ReplayStatus.Analyzed, new MatchRecord(id, fileName, playedAt ?? InSeason, branch, chests));
    }
}
