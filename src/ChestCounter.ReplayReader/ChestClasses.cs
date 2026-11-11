using Unreal.Core.Extensions;
using Unreal.Core.Models;

namespace ChestCounter;

// Any searchable class named like a chest is read, so a chest added by a game update is counted
// without a new version. The exclusions are the "chest"-named classes found in November 2026
// replays that are not chests the player opens.
internal static class ChestClasses
{
    private static readonly string[] NotChests = ["LootCannon", "Prop_", "RelicFinder", "TreasureHunt", "Ammo"];

    public static string ClassOf(string path) => NetGuidCache.InstanceClassName(path) ?? path.RemoveAllPathPrefixes();

    public static bool IsChest(string className) =>
        className.Contains("Chest", StringComparison.OrdinalIgnoreCase)
        && !NotChests.Any(word => className.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] SearchFields = ["bAlreadySearched", "SearchingPawn", "SearchedMesh"];

    // Some building pieces are named like chests too ("BP_WaddleJam_DoorwayChest_A"): only a class
    // with search fields is a chest. A field name is only sent once it is first used, so a chest
    // nobody has opened yet may only show "SearchedMesh".
    public static bool IsSearchableChest(NetFieldExportGroup group) =>
        IsChest(ClassOf(group.PathName)) && group.NetFieldExports.Any(field => field is not null && SearchFields.Contains(field.Name));

    public static string? Alias(NetFieldExportGroup group) => IsSearchableChest(group) ? GenericChest.Path : null;
}
