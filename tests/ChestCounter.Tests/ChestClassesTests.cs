using Unreal.Core.Models;

namespace ChestCounter.Tests;

// Class names and field names found in real replays of November 2026.
public sealed class ChestClassesTests
{
    [Theory]
    [InlineData("Tiered_Chest_5_Child_C")]
    [InlineData("Tiered_Chest_Athena_C")]
    [InlineData("AlwaysSpawn_RareChest_C")]
    [InlineData("B_Athena_BuriedChest_Chest_C")]
    [InlineData("B_SpritePosters_Booty_Chest_Variant2_C")]
    [InlineData("BP_Chests_Cenote_C")]
    public void Chest_classes_are_chests(string className)
    {
        Assert.True(ChestClasses.IsChest(className));
    }

    [Theory]
    [InlineData("BGA_LootCannon_StandardChest_Pirate_C")]
    [InlineData("Prop_Athena_BuriedChest_Sand_C")]
    [InlineData("Prop_Athena_BuriedChest_Dirt_C")]
    [InlineData("B_Athena_BuriedChest_RelicFinder_C")]
    [InlineData("BGA_DelilahTreasureHuntAnimatedChest_C")]
    [InlineData("Tiered_Ammo_Athena_C")]
    public void Other_chest_named_classes_are_not_chests(string className)
    {
        Assert.False(ChestClasses.IsChest(className));
    }

    [Fact]
    public void Chest_class_is_searchable_from_its_first_search_field()
    {
        var unopenedChest = Group("/Game/Building/ActorBlueprints/Containers/Tiered_Chest_5_Child.Tiered_Chest_5_Child_C", "RemoteRole", "SearchedMesh", "ReplicatedLootTier");
        var wall = Group("/Hera/Sets/WaddleJam/Blueprints/Walls/BP_WaddleJam_DoorwayChest_A.BP_WaddleJam_DoorwayChest_A_C", "RemoteRole", "bDestroyed");

        Assert.True(ChestClasses.IsSearchableChest(unopenedChest));
        Assert.Equal(GenericChest.Path, ChestClasses.Alias(unopenedChest));
        Assert.False(ChestClasses.IsSearchableChest(wall));
        Assert.Null(ChestClasses.Alias(wall));
    }

    private static NetFieldExportGroup Group(string path, params string[] fields) => new()
    {
        PathName = path,
        NetFieldExports = [.. fields.Select(name => new NetFieldExport { Name = name }), null],
    };
}
