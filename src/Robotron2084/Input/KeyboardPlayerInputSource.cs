using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// Two-stick keyboard (spec CONTROLS section + 2026-09-12/13 playtest
/// requests): WASD = movement, IJKL = 8-way aim AND fire (I up, J left,
/// K down, L right, combinations diagonal — holding re-fires every
/// PlayerAutoFireTicks while a laser slot is free), Space kept as a
/// fire alias, P = skip level (port test key — no arcade counterpart).
/// Axis sums are already exactly -1/0/1, no normalization needed.
/// </summary>
public sealed class KeyboardPlayerInputSource : IPlayerInputSource
{
    public PlayerInputState Poll()
    {
        KeyboardState state = Keyboard.GetState();

        int x = (state.IsKeyDown(Keys.D) ? 1 : 0) - (state.IsKeyDown(Keys.A) ? 1 : 0);
        int y = (state.IsKeyDown(Keys.S) ? 1 : 0) - (state.IsKeyDown(Keys.W) ? 1 : 0); // S=down increases Y, W=up decreases Y

        int ax = (state.IsKeyDown(Keys.L) ? 1 : 0) - (state.IsKeyDown(Keys.J) ? 1 : 0);
        int ay = (state.IsKeyDown(Keys.K) ? 1 : 0) - (state.IsKeyDown(Keys.I) ? 1 : 0); // K=down increases Y, I=up decreases Y

        // IJKL double as the fire trigger (round 7: "the player will no
        // longer need to press space to shoot"); Space stays as an alias.
        bool fire = state.IsKeyDown(Keys.Space)
            || state.IsKeyDown(Keys.I)
            || state.IsKeyDown(Keys.J)
            || state.IsKeyDown(Keys.K)
            || state.IsKeyDown(Keys.L);
        bool skipLevel = state.IsKeyDown(Keys.P);

        // Coin-door buttons (arcade PIA2 B4/B5): 1 = one player, 2 = two players.
        bool startOne = state.IsKeyDown(Keys.D1) || state.IsKeyDown(Keys.NumPad1);
        bool startTwo = state.IsKeyDown(Keys.D2) || state.IsKeyDown(Keys.NumPad2);

        return new PlayerInputState(new IntVector2(x, y), new IntVector2(ax, ay), fire, skipLevel, startOne, startTwo);
    }
}
