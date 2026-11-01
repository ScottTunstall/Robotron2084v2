using Microsoft.Xna.Framework;
using Robotron2084.Tuning;

namespace Robotron2084.Palette;

/// <summary>
///     The colour of the playfield wall, which changes one colour at a time. It goes through a list of colours and
///     starts again at the first when it reaches the end.
/// </summary>
/// <remarks>
///     The colours go from light to bright but never fully dark, so the list never includes black. The caller can give
///     the list (spec: "the colours in the colour cycling should be definable"), and by default it is the wall colours in
///     <see cref="WavePaletteTables" />.
/// </remarks>
public sealed class WallColourCycle
{
    private readonly IReadOnlyList<Color> _colours;
    private readonly TimeSpan _stepDuration;
    private int _colourIndex;
    private TimeSpan _elapsed;

    /// <summary>Makes a wall colour cycle that starts on the first colour.</summary>
    /// <param name="colours">The colours to go through. When it is not given, the default wall colours are used.</param>
    /// <param name="stepDuration">How long each colour lasts. When it is not given, the default wall time is used.</param>
    public WallColourCycle(IReadOnlyList<Color>? colours = null, TimeSpan? stepDuration = null)
    {
        _colours = colours ?? WavePaletteTables.DefaultWallPalette;
        _stepDuration = stepDuration ?? TimeSpan.FromMilliseconds(WavePaletteTables.WallStepDurationMilliseconds);
        CurrentColour = _colours[0];
    }

    /// <summary>The colour the wall is now.</summary>
    public Color CurrentColour { get; private set; }

    /// <summary>Moves the wall on to its next colour when the current colour has lasted long enough.</summary>
    /// <param name="gameTime">How much time has passed since the last update.</param>
    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime;
        while (_elapsed >= _stepDuration)
        {
            _elapsed -= _stepDuration;
            _colourIndex = (_colourIndex + 1) % _colours.Count;
            CurrentColour = _colours[_colourIndex];
        }
    }
}
