namespace ConsoleSnippingTool.Core;

/// Inclusive rectangle in virtual-screen pixels.
public readonly record struct Region(int Left, int Top, int Width, int Height)
{
    /// The inclusive bounding rectangle of two Corners, in either order: both Corner pixels are inside, so two
    /// clicks on one spot give a 1x1 Region.
    public static Region FromCorners(Corner a, Corner b) => new(
        Math.Min(a.X, b.X),
        Math.Min(a.Y, b.Y),
        Math.Abs(a.X - b.X) + 1,
        Math.Abs(a.Y - b.Y) + 1);
}
