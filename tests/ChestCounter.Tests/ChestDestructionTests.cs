namespace ChestCounter.Tests;

public sealed class ChestDestructionTests
{
    private const uint Chest = 7;

    private static TieredChest Export(bool? searched = null, bool? destroyed = null) =>
        new() { bAlreadySearched = searched, bDestroyed = destroyed };

    private static int CountAfter(params TieredChest[] exports)
    {
        var reader = new OwnerChestReader();
        foreach (var export in exports)
        {
            reader.Track("chest", Chest, 0, export);
        }

        return reader.DestroyedWithLootInside;
    }

    [Fact]
    public void Unopened_chest_counts()
    {
        Assert.Equal(1, CountAfter(Export(destroyed: true)));
    }

    [Fact]
    public void Opened_chest_does_not_count()
    {
        Assert.Equal(0, CountAfter(Export(searched: true), Export(destroyed: true)));
    }

    [Fact]
    public void Refilled_chest_counts()
    {
        Assert.Equal(1, CountAfter(Export(searched: true), Export(searched: false), Export(destroyed: true)));
    }

    [Fact]
    public void Chest_reported_opened_and_destroyed_together_does_not_count()
    {
        Assert.Equal(0, CountAfter(Export(searched: true, destroyed: true)));
    }

    [Fact]
    public void Destroyed_chest_counts_once()
    {
        Assert.Equal(1, CountAfter(Export(destroyed: true), Export(destroyed: true)));
    }
}
