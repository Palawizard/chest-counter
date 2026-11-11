using System.Security.Cryptography;

namespace ChestCounter.Tests;

[Trait("Category", "RealReplay")]
public sealed class ReplayAnalyzerTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Analyze_real_replay_reads_owner_openings_and_header()
    {
        var path = ReplayFixture.CopyTo(_directory, "a.replay");

        var outcome = ReplayAnalyzer.Analyze(path);

        Assert.Equal(ReplayStatus.Analyzed, outcome.Status);
        Assert.StartsWith("++Fortnite+Release-43.", outcome.Match!.Branch);
        Assert.False(string.IsNullOrEmpty(outcome.Match.Id));
        Assert.Equal(new DateTime(2026, 11, 8), outcome.Match.PlayedAt.Date);
        // The fixture's match: 11 chests opened, as LED-N's and thedackss's readers also find;
        // 2.2.2 counted 20 because 9 chests came back into view.
        Assert.Equal(11, outcome.Match.Chests);
        Assert.InRange(outcome.Match.BrokenNearby!.Value, 0, 60);
        Assert.Equal(MatchRecord.CurrentBrokenMethod, outcome.Match.BrokenMethod);
    }

    [Fact]
    public void Analyze_never_modifies_the_replay()
    {
        var path = ReplayFixture.CopyTo(_directory, "a.replay");
        var before = SHA256.HashData(File.ReadAllBytes(path));
        var writeTime = File.GetLastWriteTimeUtc(path);

        ReplayAnalyzer.Analyze(path);

        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Analyze_reports_a_replay_still_being_written_as_in_progress()
    {
        var path = ReplayFixture.CopyTo(_directory, "a.replay");
        ReplayFixture.SetLive(path, true);

        Assert.Equal(ReplayStatus.InProgress, ReplayAnalyzer.Analyze(path).Status);
    }

    [Fact]
    public void Analyze_reports_a_file_that_is_not_a_replay_as_failed()
    {
        var path = Path.Combine(_directory, "bad.replay");
        File.WriteAllBytes(path, new byte[64]);

        Assert.Equal(ReplayStatus.Failed, ReplayAnalyzer.Analyze(path).Status);
    }
}
