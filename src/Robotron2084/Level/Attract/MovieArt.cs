namespace Robotron2084.Level.Attract;

/// <summary>
/// The picture a movie object draws (notes §95.6). The names are the ROM's own
/// descriptor labels where they exist; <see cref="Quark"/> is the ROM's
/// <c>SQUARE</c> ($7EA5 — its picture table at $50C2 is the quark's SQP art),
/// <see cref="Player"/> is <c>YOU</c>, and <see cref="Cruise"/> is <c>CRUSM</c>.
/// </summary>
public enum MovieArt
{
    Mummy,
    Daddy,
    Mikey,
    Hulk,
    Brain,
    Grunt,
    Posts,
    Enforcer,
    Player,
    Quark,
    Spheroid,
    TankGrow,
    Tank,
    Points,
    Skull,
    Cruise,
}
