namespace Robotron2084.AttractMode;

/// <summary>The kinds of character in the attract movie, found by the address the scripts name them with.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Every entry was read out of the arcade's R5 ROM image: the sprite-table pointers, the animation frame
///             counts and the walk tables.
///         </item>
///         <item>Original source: the movie's object descriptors, found by the address the scripts pass to SETOB (notes §95.6)</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public static class MovieDescriptors
{
    public const int Brain = 0x7E7D;
    public const int Cruise = 0x86B0;
    public const int Daddy = 0x7E53;
    public const int Electrodes = 0x7E8F;
    public const int Enforcer = 0x7E93;
    public const int Grunt = 0x7E8B;
    public const int Hulk = 0x7E6F;
    public const int Mikey = 0x7E61;
    public const int Mommy = 0x7E41;
    public const int Points = 0x7EB5;
    public const int Quark = 0x7EA5;
    public const int Skull = 0x7EB9;
    public const int Spheroid = 0x7EA9;
    public const int Tank = 0x7EB1;
    public const int TankGrow = 0x7EAD;
    public const int You = 0x7E97;

    /// <summary>Every kind of character the movie's scripts name, with the address each is named by.</summary>
    public static readonly (int Address, MovieDescriptor Descriptor)[] All =
    [
        (Mommy, new MovieDescriptor(MovieAnimation.Mommy, 12, MovieWalk.FamilyMember)),
        (Daddy, new MovieDescriptor(MovieAnimation.Daddy, 12, MovieWalk.FamilyMember)),
        (Mikey, new MovieDescriptor(MovieAnimation.Mikey, 12, MovieWalk.FamilyMember)),
        (Hulk, new MovieDescriptor(MovieAnimation.Hulk, 12, MovieWalk.Hulk)),
        (Brain, new MovieDescriptor(MovieAnimation.Brain, 12, MovieWalk.BrainStep, 2, 8)),
        (Grunt, new MovieDescriptor(MovieAnimation.Grunt, 3)),
        (Electrodes, new MovieDescriptor(MovieAnimation.Electrodes, 36)),
        (Enforcer, new MovieDescriptor(MovieAnimation.Enforcer, 6)),
        (You, new MovieDescriptor(MovieAnimation.Player, 12, MovieWalk.BrainStep, 1, 2)),
        (Quark, new MovieDescriptor(MovieAnimation.Quark, 9)),
        (Spheroid, new MovieDescriptor(MovieAnimation.Spheroid, 8)),
        (TankGrow, new MovieDescriptor(MovieAnimation.TankGrow, 5)),
        (Tank, new MovieDescriptor(MovieAnimation.Tank, 4)),
        (Points, new MovieDescriptor(MovieAnimation.Points, 5)),
        (Skull, new MovieDescriptor(MovieAnimation.Skull, 1)),
        (Cruise, new MovieDescriptor(MovieAnimation.Cruise, 1))
    ];

    /// <summary>Resolves a SETOB operand, or null when the address is not a movie descriptor.</summary>
    public static MovieDescriptor? Resolve(int address)
    {
        foreach (var (addr, descriptor) in All)
            if (addr == address)
                return descriptor;

        return null;
    }
}
