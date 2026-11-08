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
    [InlineData(4, 100, 2, 25.0, 50.0, 150.0)]
    [InlineData(4, 15, 6, 3.75, 23.0, 38.0)]
    [InlineData(4, 100, 0, 25.0, 0.0, 100.0)]
    [InlineData(0, 0, 10, 0.0, 0.0, 0.0)]
    public void Estimate_extrapolates_the_average_to_games_without_replay(int analyzed, int chests, int withoutReplay, double average, double estimated, double total)
    {
        var summary = new Summary(analyzed, chests, 0, 0, withoutReplay);

        Assert.Equal(average, summary.Average);
        Assert.Equal(estimated, summary.EstimatedChests);
        Assert.Equal(total, summary.Total);
    }

    [Fact]
    public void Estimate_follows_the_average_when_a_new_replay_arrives()
    {
        var tracker = NewTracker();
        tracker.SetGamesWithoutReplay(9);
        AddReplay("a.replay", "id-a", 10);
        tracker.Scan(Demos);
        Assert.Equal(90, tracker.Current.EstimatedChests);

        AddReplay("b.replay", "id-b", 30);
        tracker.Scan(Demos);

        Assert.Equal(9, tracker.Current.GamesWithoutReplay);
        Assert.Equal(20, tracker.Current.Average);
        Assert.Equal(180, tracker.Current.EstimatedChests);
        Assert.Equal(220, tracker.Current.Total);
    }

    [Fact]
    public void Cancelled_scan_stops_between_replays_and_resumes_later()
    {
        using var cancellation = new CancellationTokenSource();
        var tracker = new ChestTracker(StateFile, path =>
        {
            cancellation.Cancel();
            return _outcomes[Path.GetFileName(path)];
        });
        AddReplay("a.replay", "id-a", 10);
        AddReplay("b.replay", "id-b", 30);

        tracker.Scan(Demos, cancellation: cancellation.Token);
        Assert.Equal(1, tracker.Current.GamesAnalyzed);

        tracker.Scan(Demos);
        Assert.Equal(2, tracker.Current.GamesAnalyzed);
    }

    [Fact]
    public void Broken_chests_use_the_player_share_of_openings_and_cover_games_without_replay()
    {
        var tracker = NewTracker();
        tracker.SetGamesWithoutReplay(1);
        AddReplay("a.replay", "id-a", 10, otherOpenings: 30, destroyedUnopened: 8);

        tracker.Scan(Demos);

        Assert.Equal(10, tracker.Current.Chests);
        Assert.Equal(2, tracker.Current.BrokenPerGame);
        Assert.Equal(4, tracker.Current.EstimatedBroken);
        Assert.Equal(10, tracker.Current.EstimatedChests);
        Assert.Equal(24, tracker.Current.Total);
    }

    [Fact]
    public void Old_records_are_upgraded_once_without_being_counted_twice()
    {
        WriteOldState(new MatchRecord("id-a", "a.replay", InSeason, Season, 10));
        AddReplay("a.replay", "id-a", 10, otherOpenings: 30, destroyedUnopened: 8);

        NewTracker().Scan(Demos);
        Assert.Single(_analyzed);
        _analyzed.Clear();

        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Empty(_analyzed);
        Assert.Equal(1, restarted.Current.GamesAnalyzed);
        Assert.Equal(10, restarted.Current.Chests);
        Assert.Equal(2, restarted.Current.BrokenPerGame);
    }

    [Fact]
    public void Old_record_whose_replay_is_gone_stays_counted_and_is_left_out_of_the_broken_rate()
    {
        WriteOldState(new MatchRecord("id-old", "gone.replay", InSeason, Season, 20));
        AddReplay("a.replay", "id-a", 10, otherOpenings: 30, destroyedUnopened: 8);

        var tracker = NewTracker();
        tracker.Scan(Demos);

        Assert.Equal(2, tracker.Current.GamesAnalyzed);
        Assert.Equal(30, tracker.Current.Chests);
        Assert.Equal(2, tracker.Current.BrokenPerGame);
        Assert.Equal(4, tracker.Current.EstimatedBroken);
    }

    [Fact]
    public void Games_without_replay_survive_a_restart()
    {
        NewTracker().SetGamesWithoutReplay(120);

        Assert.Equal(120, NewTracker().Current.GamesWithoutReplay);
    }

    [Fact]
    public void Matches_stay_counted_as_replays_are_deleted_one_by_one()
    {
        AddReplay("a.replay", "id-a", 10);
        AddReplay("b.replay", "id-b", 20);
        var tracker = NewTracker();
        tracker.Scan(Demos);

        File.Delete(Path.Combine(Demos, "a.replay"));
        AddReplay("c.replay", "id-c", 30);
        tracker.Scan(Demos);
        File.Delete(Path.Combine(Demos, "b.replay"));
        File.Delete(Path.Combine(Demos, "c.replay"));
        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Equal(3, restarted.Current.GamesAnalyzed);
        Assert.Equal(60, restarted.Current.Chests);
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

    private void AddReplay(string fileName, string id, int chests, string branch = Season, DateTime? playedAt = null, int otherOpenings = 0, int destroyedUnopened = 0)
    {
        File.WriteAllBytes(Path.Combine(Demos, fileName), []);
        _outcomes[fileName] = new ReplayOutcome(ReplayStatus.Analyzed, new MatchRecord(id, fileName, playedAt ?? InSeason, branch, chests, otherOpenings, destroyedUnopened));
    }

    private void WriteOldState(params MatchRecord[] matches)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        File.WriteAllText(StateFile, System.Text.Json.JsonSerializer.Serialize(new { Matches = matches, Processed = matches.Select(m => m.FileName), GamesWithoutReplay = 0 }));
    }
}
