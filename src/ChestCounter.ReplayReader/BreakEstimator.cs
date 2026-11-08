using System.Numerics;

namespace ChestCounter;

internal readonly record struct DestroyedChest(double Time, IReadOnlyDictionary<uint, Vector3> Pawns);

internal static class BreakEstimator
{
    private const float NearbyRadius = 1000f;
    private const double LootBefore = 0.6;
    private const double LootAfter = 1.2;
    private const double OpeningBefore = 1.2;
    private const double OpeningAfter = 0.6;
    private const double DrivingBefore = 3;
    private const double DrivingAfter = 1;
    private const double DrivingWeight = 0.7;

    // ponytail: "closest player within 10 m" stands in for "broke it", because replays never name who
    // destroyed a chest. Checked on 99 replays: an unrelated-loot control finds about 0.1 per match.
    // A driver's recorded position stops following the vehicle, so destructions while driving get
    // DrivingWeight instead: measured on one player, 9 matches, 4.6 per minute driving against 1.35.
    public static double Estimate(
        IReadOnlyList<DestroyedChest> destroyed,
        IReadOnlyList<double> openings,
        IReadOnlyList<(double Time, Vector3 At)> loot,
        IReadOnlySet<uint> ownerPawns,
        IReadOnlyList<double> drivingTimes)
    {
        var usable = 0;
        var credited = 0.0;
        foreach (var chest in destroyed)
        {
            if (openings.Any(time => time >= chest.Time - OpeningBefore && time <= chest.Time + OpeningAfter)
                || LootOrigin(chest.Time, loot) is not Vector3 origin)
            {
                continue;
            }

            usable++;
            if (IsClosestNearby(chest, origin, ownerPawns))
            {
                credited += 1;
            }
            else if (drivingTimes.Any(time => time >= chest.Time - DrivingBefore && time <= chest.Time + DrivingAfter))
            {
                credited += DrivingWeight;
            }
        }

        return usable == 0 ? 0 : credited * destroyed.Count / usable;
    }

    private static Vector3? LootOrigin(double time, IReadOnlyList<(double Time, Vector3 At)> loot)
    {
        var nearby = loot.Where(drop => drop.Time >= time - LootBefore && drop.Time <= time + LootAfter).Select(drop => drop.At).ToList();
        return nearby.Count == 0
            ? null
            : new Vector3(Median(nearby.Select(at => at.X)), Median(nearby.Select(at => at.Y)), Median(nearby.Select(at => at.Z)));
    }

    private static bool IsClosestNearby(DestroyedChest chest, Vector3 origin, IReadOnlySet<uint> ownerPawns)
    {
        var owner = float.MaxValue;
        var others = float.MaxValue;
        foreach (var (pawn, position) in chest.Pawns)
        {
            var distance = Vector3.Distance(position, origin);
            if (ownerPawns.Contains(pawn))
            {
                owner = Math.Min(owner, distance);
            }
            else
            {
                others = Math.Min(others, distance);
            }
        }

        return owner < NearbyRadius && owner <= others;
    }

    private static float Median(IEnumerable<float> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }
}
