using System.Diagnostics;
using System.Text.Json;

namespace ChestCounter.Tests;

public sealed class IsolatedAnalyzerTests
{
    [Fact]
    public void Child_that_never_answers_is_stopped_and_reported_as_failed()
    {
        var neverEnds = new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "ping", "-n", "60", "127.0.0.1" } };
        var watch = Stopwatch.StartNew();

        var outcome = IsolatedAnalyzer.Analyze(neverEnds, TimeSpan.FromSeconds(1));

        Assert.Equal(ReplayStatus.Failed, outcome.Status);
        Assert.Contains("longer than 1 s", outcome.Error);
        Assert.InRange(watch.Elapsed.TotalSeconds, 1, 15);
    }

    [Fact]
    public void Child_that_stops_without_a_result_is_reported_as_failed()
    {
        Assert.Equal(ReplayStatus.Failed, IsolatedAnalyzer.Parse("").Status);
        Assert.Equal(ReplayStatus.Failed, IsolatedAnalyzer.Parse("{\"Status\":2,\"Ma").Status);
    }

    [Fact]
    [Trait("Category", "RealReplay")]
    public void Replay_still_being_written_is_answered_without_a_child()
    {
        var directory = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;
        try
        {
            var path = ReplayFixture.CopyTo(directory, "live.replay");
            ReplayFixture.SetLive(path, true);

            // The executable does not exist: starting a child would throw.
            var outcome = IsolatedAnalyzer.Analyze(Path.Combine(directory, "missing.exe"), path);

            Assert.Equal(ReplayStatus.InProgress, outcome.Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "RealReplay")]
    public void Outcome_written_by_the_child_reads_back_unchanged()
    {
        var direct = ReplayAnalyzer.Analyze(ReplayFixture.Source);
        using var output = new StringWriter();

        IsolatedAnalyzer.Run(ReplayFixture.Source, output);
        var read = IsolatedAnalyzer.Parse(output.ToString());

        Assert.Equal(direct.Status, read.Status);
        Assert.Equal(direct.Match! with { Openings = null }, read.Match! with { Openings = null });
        Assert.Equal(direct.Match.Openings, read.Match.Openings);
        Assert.Equal(direct.NewChestClasses, read.NewChestClasses);
        Assert.Equal(JsonSerializer.Serialize(direct), output.ToString());
    }
}
