namespace ConsoleSnippingTool.Core;

/// The screen position of a left-button press while Armed, in virtual-screen pixels. Two Corners define a Region.
public readonly record struct Corner(int X, int Y);
