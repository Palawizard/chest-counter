namespace ChestCounter.Tests;

public sealed class HistoryTests : IDisposable
{
    private const string Season = "++Fortnite+Release-43.00";

    private readonly string _root = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;
    private readonly Dictionary<string, MatchRecord> _matches = [];

    public HistoryTests() => Directory.CreateDirectory(Demos);

    private string Demos => Path.Combine(_root, "demos");

    private string StateFile => Path.Combine(_root, "state", "state.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Last_match_is_the_latest_played_whatever_the_reading_order()
    {
        var today = DateTime.Today;
        // File names sort the other way round from the times the matches were played.
        AddReplay("a.replay", "id-late", today.AddHours(14), 27, length: 900, broken: 6.4);
        AddReplay("b.replay", "id-early", today.AddHours(9), 12);

        var tracker = NewTracker();
        tracker.Scan(Demos);
        var current = tracker.Current;

        Assert.Equal(new LastMatch(27, 6.4, today.AddHours(14).AddMinutes(15)), current.Last);
        Assert.Equal(33, current.Last!.Total);
        Assert.Equal(33 + 12, current.Today);
        Assert.Equal(2, current.TodayGames);
        Assert.Equal(33, current.Best);
    }

    [Fact]
    public void Today_leaves_out_earlier_days()
    {
        AddReplay("a.replay", "id-a", DateTime.Today.AddHours(10), 20);
        AddReplay("b.replay", "id-b", DateTime.Today.AddDays(-1).AddHours(23), 41);

        var tracker = NewTracker();
        tracker.Scan(Demos);

        Assert.Equal(20, tracker.Current.Today);
        Assert.Equal(1, tracker.Current.TodayGames);
        Assert.Equal(41, tracker.Current.Best);
    }

    [Fact]
    public void No_match_gives_no_last_match()
    {
        var tracker = NewTracker();
        tracker.Scan(Demos);

        Assert.Null(tracker.Current.Last);
        Assert.Equal(0, tracker.Current.Best);
    }

    [Fact]
    public void Match_list_is_newest_first_with_its_chests_by_kind()
    {
        // Classes as read from UnsavedReplay-2026.11.08-01.18 and similar matches.
        AddReplay("a.replay", "id-a", new DateTime(2026, 11, 8, 1, 18, 0), 5, openings:
        [
            new ChestOpening(120.4, "Tiered_Chest_Athena_C", "1A2B3C4D"),
            new ChestOpening(131.0, "AlwaysSpawn_RareChest_C", "2B3C4D5E"),
            new ChestOpening(150.2, "B_Athena_BuriedChest_Chest_C", "3C4D5E6F"),
            new ChestOpening(170.9, "B_TieredChest_Relics_C", "4D5E6F70"),
            new ChestOpening(190.1, "B_SpritePosters_Booty_Chest_Variant2_C", "5E6F7081"),
        ]);
        AddReplay("b.replay", "id-b", new DateTime(2026, 11, 9, 20, 0, 0), 0);

        var tracker = NewTracker();
        tracker.Scan(Demos);
        var lines = tracker.Matches();

        Assert.Equal(["id-b", "id-a"], lines.Select(line => line.PlayedAt == new DateTime(2026, 11, 9, 20, 0, 0) ? "id-b" : "id-a"));
        Assert.Equal(new Dictionary<string, int> { ["normal"] = 1, ["rare"] = 1, ["buried"] = 1, ["relic"] = 1, ["poster"] = 1 }, lines[1].Kinds);
        Assert.Empty(lines[0].Kinds);
    }

    [Fact]
    public void Window_settings_are_kept()
    {
        var tracker = NewTracker();
        Assert.Equal(new AppSettings(), tracker.Settings);

        tracker.ChangeSettings(settings => settings with { Overlay = true, OverlayX = 1650, OverlayY = 40, Obs = true });

        Assert.Equal(new AppSettings(true, 1650, 40, true), NewTracker().Settings);
    }

    [Fact]
    public void Notification_choice_is_kept()
    {
        var tracker = NewTracker();
        Assert.True(tracker.Notify);

        tracker.SetNotify(false);

        Assert.False(NewTracker().Notify);
    }

    private ChestTracker NewTracker() => new(StateFile, path => new ReplayOutcome(ReplayStatus.Analyzed, _matches[Path.GetFileName(path)]));

    private void AddReplay(string fileName, string id, DateTime playedAt, int chests, double length = 0, double broken = 0, IReadOnlyList<ChestOpening>? openings = null)
    {
        File.WriteAllBytes(Path.Combine(Demos, fileName), []);
        _matches[fileName] = new MatchRecord(id, fileName, playedAt, Season, chests, broken, MatchRecord.CurrentMethod, openings ?? [], length);
    }
}
