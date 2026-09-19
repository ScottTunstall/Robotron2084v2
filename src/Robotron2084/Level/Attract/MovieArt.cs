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

/// <summary>
/// How an object's MOVE opcodes advance it (notes §95.3/§95.7). <see cref="Human"/>
/// and <see cref="Hulk"/> run the ROM's ANA* walker — 8 frames a step through
/// HUMANA / HLKANA, whose entries are (image x4, dx pixels, dy pixels) and whose
/// dx is HALVED into columns by <c>DYDX</c>. <see cref="BrainStep"/> runs the
/// BR* walker: a fixed step size and nap out of the descriptor itself, cycling
/// the four-entry picture table ANATAB.
/// </summary>
public enum MovieWalk
{
    None,
    Human,
    Hulk,
    BrainStep,
}

/// <summary>
/// A movie object descriptor (ROM format: FDB picture-table, FCB image count,
/// FCB 4, then for the walkers FDB walk-L/R/D/U and FDB walk-table).
/// </summary>
/// <param name="Art">Which port texture set draws it.</param>
/// <param name="ImageCount">The ROM's image count (the walk images cycle within it).</param>
/// <param name="Walk">Which walker its MOVE opcodes use.</param>
/// <param name="StepSize">BR* walkers only: the descriptor's step byte (half-columns).</param>
/// <param name="StepNap">BR* walkers only: the descriptor's nap byte (ROM frames a step).</param>
public readonly record struct MovieDescriptor(
    MovieArt Art,
    int ImageCount,
    MovieWalk Walk = MovieWalk.None,
    int StepSize = 0,
    int StepNap = 0);

/// <summary>
/// The ROM's movie descriptors, keyed by the address the scripts pass to SETOB
/// (notes §95.6). Every entry was read out of the R5 image: the picture-table
/// pointers, the image counts and the walk tables.
/// </summary>
public static class MovieDescriptors
{
    public const int Mommy = 0x7E41;
    public const int Daddy = 0x7E53;
    public const int Mikey = 0x7E61;
    public const int Hulk = 0x7E6F;
    public const int Brain = 0x7E7D;
    public const int Grunt = 0x7E8B;
    public const int Posts = 0x7E8F;
    public const int Enforcer = 0x7E93;
    public const int You = 0x7E97;
    public const int Square = 0x7EA5;
    public const int Circle = 0x7EA9;
    public const int TankGrow = 0x7EAD;
    public const int Tank = 0x7EB1;
    public const int Points = 0x7EB5;
    public const int Skull = 0x7EB9;
    public const int Cruise = 0x86B0;

    /// <summary>Every descriptor the movie's scripts reference, by ROM address.</summary>
    public static readonly (int Address, MovieDescriptor Descriptor)[] All =
    [
        (Mommy, new MovieDescriptor(MovieArt.Mummy, 12, MovieWalk.Human)),
        (Daddy, new MovieDescriptor(MovieArt.Daddy, 12, MovieWalk.Human)),
        (Mikey, new MovieDescriptor(MovieArt.Mikey, 12, MovieWalk.Human)),
        (Hulk, new MovieDescriptor(MovieArt.Hulk, 12, MovieWalk.Hulk)),
        (Brain, new MovieDescriptor(MovieArt.Brain, 12, MovieWalk.BrainStep, 2, 8)),
        (Grunt, new MovieDescriptor(MovieArt.Grunt, 3)),
        (Posts, new MovieDescriptor(MovieArt.Posts, 36)),
        (Enforcer, new MovieDescriptor(MovieArt.Enforcer, 6)),
        (You, new MovieDescriptor(MovieArt.Player, 12, MovieWalk.BrainStep, 1, 2)),
        (Square, new MovieDescriptor(MovieArt.Quark, 9)),
        (Circle, new MovieDescriptor(MovieArt.Spheroid, 8)),
        (TankGrow, new MovieDescriptor(MovieArt.TankGrow, 5)),
        (Tank, new MovieDescriptor(MovieArt.Tank, 4)),
        (Points, new MovieDescriptor(MovieArt.Points, 5)),
        (Skull, new MovieDescriptor(MovieArt.Skull, 1)),
        (Cruise, new MovieDescriptor(MovieArt.Cruise, 1)),
    ];

    /// <summary>Resolves a SETOB operand, or null when the address is not a movie descriptor.</summary>
    public static MovieDescriptor? Resolve(int address)
    {
        foreach ((int addr, MovieDescriptor descriptor) in All)
        {
            if (addr == address)
            {
                return descriptor;
            }
        }

        return null;
    }
}
