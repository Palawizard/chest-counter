namespace ChestCounter.Tests;

// Channel indexes, times and pawns are taken from the state log of real replays
// (UnsavedReplay-2026.11.08-21.53.38 and UnsavedReplay-2026.11.08-21.38.xx).
public sealed class ChestIdentityTests
{
    private const string NormalChest = "AlwaysSpawn_NormalChest_C_UAID_387C764928FBB7FF02_e62a1cf6fa125f5c_2012538023";
    private const uint Owner = 14268;

    private static AlwaysSpawnNormalChest State(bool? searched, uint? pawn = null) =>
        new() { bAlreadySearched = searched, SearchingPawn = pawn };

    [Fact]
    public void Chest_seen_again_on_a_new_channel_is_not_opened_again()
    {
        var reader = new OwnerChestReader();
        reader.ChannelOpened(401, 214.46);
        reader.Track(NormalChest, 401, 228.15, State(true, Owner));
        reader.ChannelOpened(590, 356.86);
        reader.Track(NormalChest, 590, 356.86, State(true, Owner));

        Assert.Equal(1, reader.Openings);
    }

    [Fact]
    public void Chest_refilled_on_the_same_channel_counts_again()
    {
        var reader = new OwnerChestReader();
        reader.ChannelOpened(603, 222.64);
        reader.Track("AlwaysSpawn_NormalChest_C_UAID_387C764928FBF30C03_1246112428", 603, 374.04, State(true, Owner));
        reader.Track("AlwaysSpawn_NormalChest_C_UAID_387C764928FBF30C03_1246112428", 603, 453.24, State(false));
        reader.Track("AlwaysSpawn_NormalChest_C_UAID_387C764928FBF30C03_1246112428", 603, 593.94, State(true));

        Assert.Equal(2, reader.Openings);
    }

    [Fact]
    public void Chest_refilled_out_of_view_counts_when_opened_after_coming_back()
    {
        var reader = new OwnerChestReader();
        reader.ChannelOpened(12, 300.0);
        reader.Track("Tiered_Chest_Athena_C_refilled", 12, 329.8, State(true, 22478));
        reader.ChannelOpened(40, 485.7);
        reader.Track("Tiered_Chest_Athena_C_refilled", 40, 506.4, State(true, 15798));

        Assert.Equal(2, reader.Openings);
    }

    [Fact]
    public void Chest_whose_new_channel_starts_unsearched_counts_when_opened()
    {
        var reader = new OwnerChestReader();
        reader.ChannelOpened(7, 410.0);
        reader.Track("AlwaysSpawn_NormalChest_C_late", 7, 416.1, State(true, 30912));
        reader.ChannelOpened(9, 556.1);
        reader.Track("AlwaysSpawn_NormalChest_C_late", 9, 556.1, State(null, 32642));
        reader.Track("AlwaysSpawn_NormalChest_C_late", 9, 624.3, State(true, 12230));

        Assert.Equal(2, reader.Openings);
    }

    [Fact]
    public void Spawned_chest_back_within_150_units_is_the_same_chest()
    {
        var reader = new OwnerChestReader();
        var first = reader.SpawnedChestKey("B_TieredChest_Relics_C", 61346f, 80543f);

        Assert.Equal(first, reader.SpawnedChestKey("B_TieredChest_Relics_C", 61446f, 80443f));
        Assert.NotEqual(first, reader.SpawnedChestKey("B_TieredChest_Relics_C", 61546f, 80543f));
        Assert.NotEqual(first, reader.SpawnedChestKey("B_Athena_BuriedChest_Chest_C", 61346f, 80543f));
    }
}
