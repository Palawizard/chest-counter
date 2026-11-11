using System.Drawing;

namespace ChestCounter;

public static class OverlayPlacement
{
    public const int Margin = 24;
    private const int MinVisible = 40;

    // The saved spot when enough of the overlay stays on a screen, so it can still be grabbed;
    // otherwise (a screen was unplugged) the top right corner of the primary screen.
    public static Point Place(Point? saved, Size size, IReadOnlyList<Rectangle> workAreas, Rectangle primary)
    {
        if (saved is Point spot)
        {
            var bounds = new Rectangle(spot, size);
            if (workAreas.Any(area => Rectangle.Intersect(area, bounds) is { Width: >= MinVisible, Height: >= MinVisible }))
            {
                return spot;
            }
        }

        return new Point(primary.Right - size.Width - Margin, primary.Top + Margin);
    }
}
