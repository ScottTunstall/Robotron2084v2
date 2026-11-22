# The main menu

[Back to the README](../README.md)

The main menu is the page from which a game is started. It shows the cabinet artwork behind four rows. Each row has a name on the left and, where it has one, a value on the right.

| Row | Value | What it does |
|---|---|---|
| **PLAYER MODE** | One player, two player alternate, or two player simultaneous | Chooses how the next game is played. `Enter` starts it. |
| **GAME STYLE** | CLASSIC or EXTENDED | Chooses which game is played. The choice is saved as soon as it changes. |
| **SETTINGS** | none | Opens the [settings pages](settings.md). |
| **F10 - DEVELOPER TOOLS** | none | Opens the developer tools (attract screens, input definitions, sound test, speech test). |

## Controls

- `Up` / `Down` (or a controller's pad or left stick) move the cursor.
- `Left` / `Right` change the value of the row the cursor is on.
- `Enter` (or the controller's A button) does what the row says.
- `F1`, `F2` and `F3` start a one player, two player alternate or two player simultaneous game immediately, without moving the cursor.
- `F10` opens the developer tools from any screen.

## Player modes

- **One player.** A single player.
- **Two player alternate.** Two players take turns, as on the arcade cabinet. The game announces which player is up.
- **Two player simultaneous.** The mode is offered on the menu. Sharing the playfield between two players is not built yet.

## Game style

- **CLASSIC** is the arcade game. Wave tables, speeds, timings and behaviour are the original's and are not altered by any option on this page.
- **EXTENDED** adds features the author has written beyond the original. They are kept separate from the arcade logic and are always off in CLASSIC.

CLASSIC is the default.

## Idle behaviour

If the menu is left alone for 30 seconds it returns to the title screen and the attract sequence resumes.
