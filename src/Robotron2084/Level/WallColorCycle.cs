using Microsoft.Xna.Framework;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// The wall's colour cycling (spec: "go from light to bright, but never fully
/// dark"; "the colours in the colour cycling should be definable"). Steps
/// through a caller-supplied palette (default: 4 cyan shades) on a fixed
/// time step, wrapping forward — the palette itself never includes black.
/// </summary>
public sealed class WallColorCycle
{
    private readonly IReadOnlyList<Color> _palette;
    private readonly TimeSpan _stepDuration;
    private TimeSpan _elapsed;
    private int _index;

    public WallColorCycle(IReadOnlyList<Color>? palette = null, TimeSpan? stepDuration = null)
    {
        _palette = palette ?? GameplayConstants.DefaultWallPalette;
        _stepDuration = stepDuration ?? TimeSpan.FromMilliseconds(GameplayConstants.WallStepDurationMilliseconds);
        CurrentColor = _palette[0];
    }

    /// <summary>The wall colour for the current step.</summary>
    public Color CurrentColor { get; private set; }

    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime;
        while (_elapsed >= _stepDuration)
        {
            _elapsed -= _stepDuration;
            _index = (_index + 1) % _palette.Count;
            CurrentColor = _palette[_index];
        }
    }
}
