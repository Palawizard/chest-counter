namespace ChestCounter.Tests;

public sealed class BackupTests : IDisposable
{
    private const string Season = "++Fortnite+Release-43.00";

    private readonly string _root = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;

    public BackupTests() => Directory.CreateDirectory(Demos);

    private string Demos => Path.Combine(_root, "demos");

    private string StateDirectory => Path.Combine(_root, "state");

    private string StateFile => Path.Combine(StateDirectory, "state.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Deleted_state_comes_back_from_the_backup()
    {
        CountTwoMatches();
        File.Delete(StateFile);

        var tracker = NewTracker();

        Assert.True(tracker.RestoredFromBackup);
        Assert.Equal(2, tracker.Current.GamesAnalyzed);
        Assert.Equal(21, tracker.Current.Chests);
    }

    [Fact]
    public void Truncated_state_comes_back_from_the_backup_and_is_set_aside()
    {
        CountTwoMatches();
        var saved = File.ReadAllText(StateFile);
        File.WriteAllText(StateFile, saved[..(saved.Length / 2)]);

        var tracker = NewTracker();

        Assert.True(tracker.RestoredFromBackup);
        Assert.Equal(21, tracker.Current.Chests);
        var setAside = Assert.Single(Directory.GetFiles(StateDirectory, "state.json.unreadable-*"));
        Assert.Equal(saved[..(saved.Length / 2)], File.ReadAllText(setAside));
    }

    [Fact]
    public void Normal_start_does_not_report_a_restore()
    {
        CountTwoMatches();

        Assert.False(NewTracker().RestoredFromBackup);
    }

    [Fact]
    public void State_from_2_2_is_copied_once_before_it_is_recounted()
    {
        Directory.CreateDirectory(StateDirectory);
        const string From222 = """{"Matches":[{"Id":"id-a","FileName":"a.replay","PlayedAt":"2026-11-07T20:10:00","Branch":"++Fortnite+Release-43.00","Chests":20,"BrokenNearby":3.5,"BrokenMethod":2}],"Processed":["a.replay"],"GamesWithoutReplay":25}""";
        File.WriteAllText(StateFile, From222);
        File.WriteAllBytes(Path.Combine(Demos, "a.replay"), []);

        var tracker = new ChestTracker(StateFile, _ => Outcome("id-a", "a.replay", 11));
        tracker.Scan(Demos);
        NewTracker();

        Assert.Equal(From222, File.ReadAllText(Path.Combine(StateDirectory, "state.2.2.json")));
    }

    private void CountTwoMatches()
    {
        File.WriteAllBytes(Path.Combine(Demos, "a.replay"), []);
        File.WriteAllBytes(Path.Combine(Demos, "b.replay"), []);
        NewTracker().Scan(Demos);
    }

    private ChestTracker NewTracker() => new(StateFile, path => Path.GetFileName(path) == "a.replay" ? Outcome("id-a", "a.replay", 11) : Outcome("id-b", "b.replay", 10));

    private static ReplayOutcome Outcome(string id, string fileName, int chests) =>
        new(ReplayStatus.Analyzed, new MatchRecord(id, fileName, new DateTime(2026, 11, 8), Season, chests, 2, MatchRecord.CurrentMethod));
}
