namespace Robotron2084.Entities;

/// <summary>One strip to draw: the source index (row or column) and its top-left.</summary>
public readonly record struct Strip(int SourceIndex, int X, int Y);
