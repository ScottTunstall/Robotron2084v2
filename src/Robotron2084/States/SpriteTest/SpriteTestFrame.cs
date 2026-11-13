using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.States.SpriteTest;

/// <summary>One animation frame of a <see cref="SpriteTestCycle" />, and how long it is shown.</summary>
/// <param name="Texture">The animation frame.</param>
/// <param name="HoldClockUnits">
///     How long it is shown, in <see cref="Robotron2084.Core.ArcadeClock.ClockUnitsPerSecond">clock units</see>.
/// </param>
public readonly record struct SpriteTestFrame(Texture2D Texture, int HoldClockUnits);
