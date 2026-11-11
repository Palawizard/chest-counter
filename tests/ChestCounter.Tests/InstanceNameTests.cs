using Unreal.Core.Models;

namespace ChestCounter.Tests;

// Instance names found in real replays of November 2026.
public sealed class InstanceNameTests
{
    [Theory]
    [InlineData("Tiered_Chest_Athena5_0c562a1de7319e3c_15", "Tiered_Chest_Athena_C")]
    [InlineData("Tiered_Chest_Athena2_6e02a0a859c897db_2", "Tiered_Chest_Athena_C")]
    [InlineData("Tiered_Chest_Athena47_UAID_387C764928FB54FC02_48ed44e940bfbeef_1632776713", "Tiered_Chest_Athena_C")]
    [InlineData("Tiered_Chest_Athena_C_0c562a1de7319e3c_2", "Tiered_Chest_Athena_C")]
    [InlineData("AlwaysSpawn_NormalChest_C_UAID_387C764928FBB7FF02_e62a1cf6fa125f5c_2012538023", "AlwaysSpawn_NormalChest_C")]
    [InlineData("/Game/Athena/Maps/Athena_Terrain.Tiered_Chest_Athena3_6e02a0a859c897db_5", "Tiered_Chest_Athena_C")]
    public void Instance_name_gives_its_class(string path, string expected)
    {
        Assert.Equal(expected, NetGuidCache.InstanceClassName(path));
    }

    [Theory]
    [InlineData("B_TieredChest_Relics_C")]
    [InlineData("/RelicChest/Gameplay/B_TieredChest_Relics.B_TieredChest_Relics_C")]
    public void Class_path_is_not_an_instance(string path)
    {
        Assert.Null(NetGuidCache.InstanceClassName(path));
    }
}
