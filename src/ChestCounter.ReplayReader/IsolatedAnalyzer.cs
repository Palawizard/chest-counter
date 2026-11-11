using System.Diagnostics;
using System.Text.Json;

namespace ChestCounter;

// Each replay is read by a child copy of the app: a replay that makes the reader loop cannot freeze
// the scan, and the memory of the read goes back to Windows when the child exits.
public static class IsolatedAnalyzer
{
    public const string Argument = "--read-replay";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static ReplayOutcome Analyze(string executable, string path)
    {
        if (ReplayAnalyzer.Peek(path) is { } quick)
        {
            return quick;
        }

        var start = new ProcessStartInfo(executable) { ArgumentList = { Argument, path } };
        return Analyze(start, Timeout);
    }

    // Runs in the child: writes the outcome for the parent to read.
    public static void Run(string path, TextWriter output)
    {
        output.Write(JsonSerializer.Serialize(ReplayAnalyzer.Analyze(path)));
        output.Flush();
    }

    internal static ReplayOutcome Analyze(ProcessStartInfo start, TimeSpan timeout)
    {
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("the replay reader did not start");
        try
        {
            child.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already finished, or not allowed: the read still happens at normal priority.
        }

        var output = child.StandardOutput.ReadToEndAsync();
        if (!child.WaitForExit(timeout))
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
            return new ReplayOutcome(ReplayStatus.Failed, Error: $"reading took longer than {timeout.TotalSeconds:F0} s");
        }

        return Parse(output.GetAwaiter().GetResult());
    }

    internal static ReplayOutcome Parse(string output)
    {
        try
        {
            return JsonSerializer.Deserialize<ReplayOutcome>(output) ?? new ReplayOutcome(ReplayStatus.Failed, Error: "the replay reader returned nothing");
        }
        catch (JsonException)
        {
            return new ReplayOutcome(ReplayStatus.Failed, Error: "the replay reader stopped before giving a result");
        }
    }
}
