using System.Drawing;

namespace ChestCounter.Tests;

public sealed class OverlayPlacementTests
{
    private static readonly Size Overlay = new(230, 80);
    private static readonly Rectangle Primary = new(0, 0, 1920, 1040);
    private static readonly Rectangle Left = new(-2560, 0, 2560, 1400);

    [Fact]
    public void Saved_spot_on_a_screen_is_kept()
    {
        Assert.Equal(new Point(-2400, 60), OverlayPlacement.Place(new Point(-2400, 60), Overlay, [Primary, Left], Primary));
    }

    [Fact]
    public void Spot_on_an_unplugged_screen_comes_back_to_the_primary_one()
    {
        Assert.Equal(new Point(1920 - 230 - 24, 24), OverlayPlacement.Place(new Point(-2400, 60), Overlay, [Primary], Primary));
    }

    [Fact]
    public void Spot_mostly_off_screen_comes_back()
    {
        Assert.Equal(new Point(1666, 24), OverlayPlacement.Place(new Point(1900, 500), Overlay, [Primary], Primary));
        Assert.Equal(new Point(1850, 500), OverlayPlacement.Place(new Point(1850, 500), Overlay, [Primary], Primary));
    }

    [Fact]
    public void First_use_goes_to_the_top_right_corner()
    {
        Assert.Equal(new Point(1666, 24), OverlayPlacement.Place(null, Overlay, [Primary], Primary));
    }
}
