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

    private static double Estimate(DestroyedChest[] destroyed, double[]? openings = null, double[]? lootTimes = null, double[]? driving = null) =>
        BreakEstimator.Estimate(destroyed, openings ?? [], (lootTimes ?? destroyed.Select(d => d.Time).ToArray()).Select(t => (t, Chest)).ToList(), OwnerPawns, driving ?? []);

    [Fact]
    public void Destruction_while_driving_gets_the_driving_weight()
    {
        Assert.Equal(0.7, Estimate([At(10, (Owner, 200))], driving: [9]), 3);
    }

    [Fact]
    public void Closest_owner_counts_fully_even_while_driving()
    {
        Assert.Equal(1, Estimate([At(10, (Owner, 3))], driving: [9]));
    }

    [Theory]
    [InlineData(6.9)]
    [InlineData(11.1)]
    public void Driving_outside_the_window_does_not_count(double drivingAt)
    {
        Assert.Equal(0, Estimate([At(10, (Owner, 200))], driving: [drivingAt]));
    }

    [Theory]
    [InlineData(7.0)]
    [InlineData(11.0)]
    public void Driving_at_the_window_edges_counts(double drivingAt)
    {
        Assert.Equal(0.7, Estimate([At(10, (Owner, 200))], driving: [drivingAt]), 3);
    }

    [Fact]
    public void Driving_credit_is_scaled_like_the_rest()
    {
        var destroyed = new[] { At(10, (Owner, 200)), At(50, (Owner, 3)) };

        Assert.Equal(1.4, Estimate(destroyed, lootTimes: [10], driving: [9]), 3);
    }

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
