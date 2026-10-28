namespace ConsoleSnippingTool.Core;

/// Inclusive rectangle in virtual-screen pixels.
public readonly record struct Region(int Left, int Top, int Width, int Height);
