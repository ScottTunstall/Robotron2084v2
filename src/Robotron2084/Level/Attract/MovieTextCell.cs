namespace Robotron2084.Level.Attract;

/// <summary>
/// One character the movie has printed: where (arcade pixels / rows) and in which
/// palette slot. The ROM blits characters straight onto the screen; the port
/// keeps them as a layer so the renderer can draw them with the arcade font.
/// </summary>
public readonly record struct MovieTextCell(int X, int Y, char Character, int Slot);
