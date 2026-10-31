using Microsoft.Xna.Framework;
using Robotron2084.Tuning;

namespace Robotron2084.Palette;

/// <summary>
///     The wall's colour cycling (spec: "go from light to bright, but never fully
///     dark"; "the colours in the colour cycling should be definable"). Steps
///     through a caller-supplied palette (default: 4 cyan shades) on a fixed
///     time step, wrapping forward — the palette itself never includes black.
/// </summary>
public sealed class WallColourCycle
{
    private readonly IReadOnlyList<Color> _colours;
    private readonly TimeSpan _stepDuration;
    private int _colourIndex;
    private TimeSpan _elapsed;

    public WallColourCycle(IReadOnlyList<Color>? colours = null, TimeSpan? stepDuration = null)
    {
        _colours = colours ?? WavePaletteTables.DefaultWallPalette;
        _stepDuration = stepDuration ?? TimeSpan.FromMilliseconds(WavePaletteTables.WallStepDurationMilliseconds);
        CurrentColour = _colours[0];
    }

    /// <summary>The wall colour for the current step.</summary>
    public Color CurrentColour { get; private set; }

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
