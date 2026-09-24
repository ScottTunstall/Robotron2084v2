namespace Robotron2084.Level.Attract;

/// <summary>
/// One EXP: the ROM removes the object (`KILLOF`) and starts the strip explosion
/// with the picture the object was showing at the object's own corner, its centre
/// row forced to ACTHIT+6 (notes §95.5).
/// </summary>
public readonly record struct MovieExplosion(MovieAnimation Animation, int ImageIndex, int Column, int Row);