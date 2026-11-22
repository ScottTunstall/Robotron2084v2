# Define inputs

[Back to the README](../README.md) · [Main menu](main-menu.md)

DEFINE INPUTS is the page where the keys, controller buttons and sticks that control the game are chosen. It is opened from the [main menu](main-menu.md), or from the developer tools (`F10`).

The arcade cabinet has fixed wiring, so this page is specific to this port.

## The lines

The page has one line for each direction a player can move or shoot, and one line for pause:

| Block | Lines |
|---|---|
| **Player 1** | MOVE UP, DOWN, LEFT, RIGHT, then SHOOT UP, DOWN, LEFT, RIGHT |
| **Player 2** | The same eight lines |
| **Pause** | One line |

Player 2's block is below player 1's. `Up` and `Down` scroll through the page, skipping the blank lines between the blocks.

Each MOVE and SHOOT line holds two bindings: one for the **keyboard** and one for a **controller stick**. Either one works at any time, so a player can use the keyboard and a controller without changing anything. The pause line holds a single binding, on whichever device was used to set it.

## The factory scheme

| Action | Player 1 keyboard | Player 2 keyboard | Controller |
|---|---|---|---|
| Move up, down, left, right | `W` `S` `A` `D` | `Up` `Down` `Left` `Right` | Left stick |
| Shoot up, down, left, right | `I` `K` `J` `L` | `NumPad8` `NumPad5` `NumPad4` `NumPad6` | Right stick |
| Pause | `P` | | |

Player 1 uses the first controller and player 2 uses the second.

## Changing an input

Setting an input takes two steps. This is deliberate, because otherwise the cursor keys could never be assigned: they are how the page is scrolled.

1. Move to the line and press `Enter`. The line shows **PRESS AN INPUT**.
2. Press the key, controller button or stick direction to assign. It becomes the binding for that line, and the highlight moves on to the next line.

While a line is waiting for an input, scrolling is switched off.

## Controls on this page

| Key | Action |
|---|---|
| `Up` / `Down` | Move between lines |
| `Enter` | Arm the highlighted line for a new input |
| `Del` | Clear the highlighted line (both its keyboard and its stick binding) |
| `R` | Restore every line, for both players and for pause, to the factory scheme |
| `F10` | Save and return |

Changes are saved when they are made and again when the page is left.

## Inputs that cannot be changed

These are fixed and are not listed on the page:

- The controller's `A` button and right trigger also fire.
- `1` and `2` (and `NumPad1` and `NumPad2`) start a one or two player game. On the controller these are the Start and Back buttons.
- `Insert` skips the current wave.
- The function keys, described in the [main menu](main-menu.md) and the [README](../README.md).

## Where the inputs are stored

Inputs are saved to `%LocalAppData%\Robotron2084\controls.ini`. The file can be edited by hand and is read when the game starts. Game settings are kept separately in `settings.ini` (see [Settings](settings.md)).
