using System.Text;

namespace ChestCounter.Tests;

// Builds temporary variants of a real replay. The source fixture is git-ignored because it is a
// personal recording; see the README for how to provide one.
internal static class ReplayFixture
{
    public static string Source => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "sample.replay"));

    public static string CopyTo(string directory, string fileName)
    {
        Assert.True(File.Exists(Source), $"Missing real replay fixture: {Source}");
        var target = Path.Combine(directory, fileName);
        File.Copy(Source, target);
        return target;
    }

    public static void SetLive(string path, bool live)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        stream.Position = ReplayAnalyzer.LiveFlagOffset(stream)!.Value;
        stream.Write(BitConverter.GetBytes(live ? 1u : 0u));
    }

    public static void SetOutOfSeason(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var branch = bytes.AsSpan().IndexOf("Release-43."u8);
        Assert.True(branch >= 0, "fixture has no 43.x branch");
        Encoding.ASCII.GetBytes("Release-42.").CopyTo(bytes, branch);
        using (var stream = new MemoryStream(bytes))
        {
            var timestampOffset = (int)ReplayAnalyzer.LiveFlagOffset(stream)!.Value + 4;
            BitConverter.GetBytes(new DateTime(2026, 10, 15).ToBinary()).CopyTo(bytes, timestampOffset);
        }

        File.WriteAllBytes(path, bytes);
    }
}
