namespace ChestCounter;

// Total is what the player reads for a match: chests opened plus the broken ones estimated, rounded.
public sealed record LastMatch(int Chests, double Broken, DateTime EndedAt)
{
    public int Total => MatchHistory.Total(Chests, Broken);
}

// Kinds counts the opened chests by kind ("normal", "rare", "buried", "relic", "poster").
public sealed record MatchLine(DateTime PlayedAt, int Chests, double Broken, IReadOnlyDictionary<string, int> Kinds);

internal static class MatchHistory
{
    // Matches are dated by their start; a match recorded before 2.3 has no length and ends at its start.
    public static LastMatch? Last(IEnumerable<MatchRecord> matches) =>
        matches.MaxBy(m => m.PlayedAt) is { } last
            ? new LastMatch(last.Chests, last.BrokenNearby ?? 0, last.PlayedAt.AddSeconds(last.Length ?? 0))
            : null;

    public static int Total(int chests, double broken) => (int)Math.Round(chests + broken, MidpointRounding.AwayFromZero);

    public static int On(IEnumerable<MatchRecord> matches, DateTime day) => matches.Where(m => m.PlayedAt.Date == day.Date).Sum(Total);

    public static int Best(IEnumerable<MatchRecord> matches) => matches.Select(Total).DefaultIfEmpty(0).Max();

    private static int Total(MatchRecord match) => Total(match.Chests, match.BrokenNearby ?? 0);

    public static List<MatchLine> Lines(IEnumerable<MatchRecord> matches) => matches
        .OrderByDescending(m => m.PlayedAt)
        .Select(m => new MatchLine(m.PlayedAt, m.Chests, m.BrokenNearby ?? 0, Kinds(m)))
        .ToList();

    private static Dictionary<string, int> Kinds(MatchRecord match) => (match.Openings ?? [])
        .GroupBy(opening => ChestClasses.KindOf(opening.Class))
        .ToDictionary(kind => kind.Key, kind => kind.Count());
}
