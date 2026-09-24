namespace Robotron2084.Level.Attract;

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
