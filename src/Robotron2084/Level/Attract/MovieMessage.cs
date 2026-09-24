namespace Robotron2084.Level.Attract;

/// <summary>A MESS popup — one of the ROM's message strings in the score row.</summary>
public readonly record struct MovieMessage(int X, int Y, string Text, int Slot);
