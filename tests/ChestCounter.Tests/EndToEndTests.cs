namespace ChestCounter.Tests;

[Trait("Category", "RealReplay")]
public sealed class EndToEndTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly string _root = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;

    public EndToEndTests() => Directory.CreateDirectory(Demos);

    private string Demos => Path.Combine(_root, "demos");

    private string StateFile => Path.Combine(_root, "state.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Real_replay_is_ignored_while_live_then_counted_once()
    {
        var path = ReplayFixture.CopyTo(Demos, "a.replay");
        ReplayFixture.SetLive(path, true);
        var tracker = new ChestTracker(StateFile, ReplayAnalyzer.Analyze);

        tracker.Scan(Demos);
        Assert.Equal(0, tracker.Current.GamesAnalyzed);
        Assert.Equal(1, tracker.Current.InProgress);

        ReplayFixture.SetLive(path, false);
        tracker.Scan(Demos);
        tracker.Scan(Demos);

        Assert.Equal(1, tracker.Current.GamesAnalyzed);
        Assert.Equal(0, tracker.Current.InProgress);
    }

    [Fact]
    public void Real_replay_outside_the_season_is_excluded()
    {
        var path = ReplayFixture.CopyTo(Demos, "old.replay");
        ReplayFixture.SetOutOfSeason(path);
        var tracker = new ChestTracker(StateFile, ReplayAnalyzer.Analyze);

        tracker.Scan(Demos);

        Assert.Equal(0, tracker.Current.GamesAnalyzed);
        Assert.Equal(0, tracker.Current.Failed);
    }

    [Fact]
    public void Replay_dropped_while_watching_is_counted_once_and_a_restart_adds_nothing()
    {
        var tracker = new ChestTracker(StateFile, ReplayAnalyzer.Analyze);
        var expected = ReplayAnalyzer.Analyze(ReplayFixture.Source).Match!.Chests;
        using (new ReplayFolderWatcher(Demos, _ => tracker.Scan(Demos), e => Assert.Fail(e.ToString()), TimeSpan.FromMilliseconds(200), TimeSpan.FromHours(1)))
        {
            ReplayFixture.CopyTo(Demos, "new.replay");
            WaitUntil(() => tracker.Current.GamesAnalyzed == 1);
            Thread.Sleep(600);
        }

        Assert.Equal(expected, tracker.Current.Chests);

        var restarted = new ChestTracker(StateFile, _ => throw new InvalidOperationException("must not reanalyze"));
        restarted.Scan(Demos);
        Assert.Equal(1, restarted.Current.GamesAnalyzed);
        Assert.Equal(expected, restarted.Current.Chests);
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out waiting for the watcher");
            Thread.Sleep(50);
        }
    }
}
