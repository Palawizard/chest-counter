using System.Numerics;

namespace ChestCounter.Tests;

public sealed class BreakEstimatorTests
{
    private const uint Owner = 1;
    private const uint Other = 2;
    private static readonly HashSet<uint> OwnerPawns = [Owner];
    private static readonly Vector3 Chest = new(1000, 1000, 0);

    private static DestroyedChest At(double time, params (uint Pawn, float Metres)[] pawns) =>
        new(time, pawns.ToDictionary(p => p.Pawn, p => Chest + new Vector3(p.Metres * 100, 0, 0)));

    private static double Estimate(DestroyedChest[] destroyed, double[]? openings = null, double[]? lootTimes = null) =>
        BreakEstimator.Estimate(destroyed, openings ?? [], (lootTimes ?? destroyed.Select(d => d.Time).ToArray()).Select(t => (t, Chest)).ToList(), OwnerPawns);

    [Fact]
    public void Chest_destroyed_with_the_owner_closest_and_nearby_counts()
    {
        Assert.Equal(1, Estimate([At(10, (Owner, 3), (Other, 40))]));
    }

    [Fact]
    public void Chest_destroyed_far_from_the_owner_does_not_count()
    {
        Assert.Equal(0, Estimate([At(10, (Owner, 11))]));
    }

    [Fact]
    public void Chest_destroyed_with_someone_closer_does_not_count()
    {
        Assert.Equal(0, Estimate([At(10, (Owner, 8), (Other, 2))]));
    }

    [Fact]
    public void Chest_destroyed_after_the_owner_left_does_not_count()
    {
        Assert.Equal(0, Estimate([At(10, (Other, 2))]));
    }

    [Fact]
    public void Destruction_during_an_opening_is_left_out_and_scaled_from_the_others()
    {
        var destroyed = new[] { At(10, (Owner, 3)), At(50, (Owner, 3)) };

        Assert.Equal(2, Estimate(destroyed, openings: [10.5]));
    }

    [Fact]
    public void Destruction_without_loot_is_left_out_and_scaled_from_the_others()
    {
        var destroyed = new[] { At(10, (Owner, 3)), At(50, (Owner, 30)) };

        Assert.Equal(2, Estimate(destroyed, lootTimes: [10]));
    }

    [Fact]
    public void No_usable_destruction_gives_zero()
    {
        Assert.Equal(0, Estimate([At(10, (Owner, 3))], lootTimes: []));
    }
}
