using Unreal.Core.Attributes;
using Unreal.Core.Contracts;
using Unreal.Core.Models.Enums;

namespace ChestCounter;

// The library only loads export groups from assemblies whose name contains "ReplayReader",
// which is why this project carries that name.
public abstract class SearchableChest : INetFieldExportGroup
{
    [NetFieldExport("bAlreadySearched", RepLayoutCmdType.PropertyBool)]
    public bool? bAlreadySearched { get; set; }

    [NetFieldExport("SearchingPawn", RepLayoutCmdType.PropertyObject)]
    public uint? SearchingPawn { get; set; }
}

[NetFieldExportGroup("/Game/Building/ActorBlueprints/Containers/Tiered_Chest_Athena.Tiered_Chest_Athena_C", ParseMode.Normal)]
public sealed class TieredChest : SearchableChest;

[NetFieldExportGroup("/Game/Building/ActorBlueprints/Containers/AlwaysSpawn_NormalChest.AlwaysSpawn_NormalChest_C", ParseMode.Normal)]
public sealed class AlwaysSpawnNormalChest : SearchableChest;

[NetFieldExportGroup("/Game/Building/ActorBlueprints/Containers/AlwaysSpawn_RareChest.AlwaysSpawn_RareChest_C", ParseMode.Normal)]
public sealed class AlwaysSpawnRareChest : SearchableChest;

[NetFieldExportGroup("/Game/Athena/Items/ForagedItems/BuriedChest/B_Athena_BuriedChest_Chest.B_Athena_BuriedChest_Chest_C", ParseMode.Normal)]
public sealed class BuriedChest : SearchableChest;

[NetFieldExportGroup("/RelicChest/Gameplay/B_TieredChest_Relics.B_TieredChest_Relics_C", ParseMode.Normal)]
public sealed class RelicChest : SearchableChest;

[NetFieldExportGroup("/SpritePosters/Gameplay/RelicChests/B_Delilah_TieredChest_Relics_Variant1.B_Delilah_TieredChest_Relics_Variant1_C", ParseMode.Normal)]
public sealed class RelicChestVariant1 : SearchableChest;

[NetFieldExportGroup("/SpritePosters/Gameplay/RelicChests/B_Delilah_TieredChest_Relics_Variant2.B_Delilah_TieredChest_Relics_Variant2_C", ParseMode.Normal)]
public sealed class RelicChestVariant2 : SearchableChest;

[NetFieldExportGroup("/SpritePosters/Blueprints/Items/Booty/B_SpritePosters_Booty_Chest.B_SpritePosters_Booty_Chest_C", ParseMode.Normal)]
public sealed class SpritePosterChest : SearchableChest;

[NetFieldExportGroup("/SpritePosters/Blueprints/Items/Booty/B_SpritePosters_Booty_Chest_Variant1.B_SpritePosters_Booty_Chest_Variant1_C", ParseMode.Normal)]
public sealed class SpritePosterChestVariant1 : SearchableChest;
