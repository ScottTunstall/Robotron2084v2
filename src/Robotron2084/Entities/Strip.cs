namespace Robotron2084.Entities;

/// <summary>One strip to draw: which row or column of the sprite it is, and where its top-left corner goes.</summary>
public readonly record struct Strip(int SourceIndex, int X, int Y);
