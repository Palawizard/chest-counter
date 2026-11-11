namespace ChestCounter.Tests;

// The 2.2 to 2.3 move: matches counted by the old analysis are read again while their replay exists;
// those whose replay is gone become matches without a replay.
public sealed class MigrationTests : IDisposable
{
    private const string Season = "++Fortnite+Release-43.00";

    private readonly string _root = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;
    private readonly Dictionary<string, ReplayOutcome> _outcomes = [];
    private readonly List<string> _analyzed = [];

    public MigrationTests() => Directory.CreateDirectory(Demos);

    private string Demos => Path.Combine(_root, "demos");

    private string StateFile => Path.Combine(_root, "state", "state.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Old_matches_without_replay_move_once_and_keep_the_entered_games()
    {
        WriteStateFrom222(("id-a", "a.replay", 20), ("id-gone", "gone.replay", 30));
        AddReplay("a.replay", "id-a", 11);

        var tracker = NewTracker();
        tracker.Scan(Demos);
        tracker.Scan(Demos);
        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Equal(1, restarted.Current.GamesAnalyzed);
        Assert.Equal(11, restarted.Current.Chests);
        Assert.Equal(25 + 1, restarted.Current.GamesWithoutReplay);
        Assert.Equal(["a.replay"], _analyzed);
    }

    [Fact]
    public void Match_read_by_this_version_stays_counted_when_its_replay_is_deleted()
    {
        WriteStateFrom222();
        AddReplay("a.replay", "id-a", 11);
        AddReplay("b.replay", "id-b", 9);
        NewTracker().Scan(Demos);
        File.Delete(Path.Combine(Demos, "a.replay"));

        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Equal(2, restarted.Current.GamesAnalyzed);
        Assert.Equal(20, restarted.Current.Chests);
        Assert.Equal(25, restarted.Current.GamesWithoutReplay);
    }

    [Fact]
    public void Empty_replay_folder_moves_nothing()
    {
        WriteStateFrom222(("id-gone", "gone.replay", 30));

        var tracker = NewTracker();
        tracker.Scan(Demos);

        Assert.Equal(1, tracker.Current.GamesAnalyzed);
        Assert.Equal(30, tracker.Current.Chests);
        Assert.Equal(25, tracker.Current.GamesWithoutReplay);
    }

    [Fact]
    public void Missing_replay_folder_moves_nothing()
    {
        WriteStateFrom222(("id-gone", "gone.replay", 30));
        var tracker = NewTracker();

        Assert.Throws<DirectoryNotFoundException>(() => tracker.Scan(Path.Combine(_root, "moved")));

        Assert.Equal(1, tracker.Current.GamesAnalyzed);
        Assert.Equal(25, tracker.Current.GamesWithoutReplay);
    }

    [Fact]
    public void Interrupted_scan_moves_nothing()
    {
        WriteStateFrom222(("id-gone", "gone.replay", 30));
        AddReplay("a.replay", "id-a", 11);
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        var tracker = NewTracker();
        tracker.Scan(Demos, cancellation: stop.Token);

        Assert.Equal(1, tracker.Current.GamesAnalyzed);
        Assert.Equal(25, tracker.Current.GamesWithoutReplay);
    }

    [Fact]
    public void Moved_match_whose_replay_comes_back_is_read_and_leaves_the_estimate()
    {
        WriteStateFrom222(("id-a", "a.replay", 20), ("id-gone", "gone.replay", 30));
        AddReplay("a.replay", "id-a", 11);
        NewTracker().Scan(Demos);

        AddReplay("gone.replay", "id-gone", 18);
        var restarted = NewTracker();
        restarted.Scan(Demos);

        Assert.Equal(2, restarted.Current.GamesAnalyzed);
        Assert.Equal(11 + 18, restarted.Current.Chests);
        Assert.Equal(25, restarted.Current.GamesWithoutReplay);
    }

    [Fact]
    public void Games_without_replay_never_drop_below_zero()
    {
        WriteStateFrom222(("id-a", "a.replay", 20), ("id-gone", "gone.replay", 30));
        AddReplay("a.replay", "id-a", 11);
        var tracker = NewTracker();
        tracker.Scan(Demos);
        tracker.SetGamesWithoutReplay(0);

        AddReplay("gone.replay", "id-gone", 18);
        tracker.Scan(Demos);

        Assert.Equal(0, tracker.Current.GamesWithoutReplay);
    }

    [Fact]
    public void Recount_shows_its_message_once_with_the_numbers_before_and_after()
    {
        WriteStateFrom222(("id-a", "a.replay", 20), ("id-gone", "gone.replay", 30));
        AddReplay("a.replay", "id-a", 11);

        var tracker = NewTracker();
        Assert.Null(tracker.Notice);
        tracker.Scan(Demos);

        var notice = tracker.Notice!;
        Assert.Equal((50, 11, 1), (notice.ReadBefore, notice.ReadAfter, notice.Moved));
        Assert.NotNull(NewTracker().Notice);

        tracker.DismissNotice();
        var restarted = NewTracker();
        restarted.Scan(Demos);
        Assert.Null(restarted.Notice);
    }

    [Fact]
    public void Recount_that_changes_nothing_shows_no_message()
    {
        WriteStateFrom222(("id-a", "a.replay", 11));
        AddReplay("a.replay", "id-a", 11);

        var tracker = NewTracker();
        tracker.Scan(Demos);

        Assert.Null(tracker.Notice);
    }

    [Fact]
    public void First_install_shows_no_message()
    {
        AddReplay("a.replay", "id-a", 11);

        var tracker = NewTracker();
        tracker.Scan(Demos);

        Assert.Null(tracker.Notice);
    }

    private ChestTracker NewTracker() => new(StateFile, path =>
    {
        var name = Path.GetFileName(path);
        _analyzed.Add(name);
        return _outcomes[name];
    });

    private void AddReplay(string fileName, string id, int chests)
    {
        File.WriteAllBytes(Path.Combine(Demos, fileName), []);
        _outcomes[fileName] = new ReplayOutcome(ReplayStatus.Analyzed, new MatchRecord(id, fileName, new DateTime(2026, 11, 8), Season, chests, 2, MatchRecord.CurrentMethod));
    }

    // The exact shape 2.2.2 writes, with 25 matches entered by the player.
    private void WriteStateFrom222(params (string Id, string FileName, int Chests)[] matches)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        var records = string.Join(",", matches.Select(m => $$"""{"Id":"{{m.Id}}","FileName":"{{m.FileName}}","PlayedAt":"2026-11-07T20:10:00","Branch":"{{Season}}","Chests":{{m.Chests}},"BrokenNearby":3.5,"BrokenMethod":2}"""));
        var processed = string.Join(",", matches.Select(m => $"\"{m.FileName}\""));
        File.WriteAllText(StateFile, $$"""{"Matches":[{{records}}],"Processed":[{{processed}}],"GamesWithoutReplay":25}""");
    }
}
