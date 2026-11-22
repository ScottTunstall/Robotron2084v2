# Robotron: 2084 — a from-source port in MonoGame

<img width="1916" height="1200" alt="title page 1" src="https://github.com/user-attachments/assets/ad77ae6d-e5de-407f-a417-c2a1043a598c" />

**Robotron: 2084** (Williams Electronics, 1982) re-implemented in C# / MonoGame
(`net10.0`, desktop OpenGL). It is not an emulator and not a re-skin. No 6809 code
is run: the robots, timers, explosions and colours are re-derived from the original
assembly listings and checked against the shipped ROM image and the author's own
annotated 6809 disassembly.

## Quick start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Build and run the game from the repository folder:

   ```bash
   dotnet build Robotron2084.slnx
   ./src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe
   ```

3. On the [main menu](docs/main-menu.md), press `Enter` to start a one player game, or `F2` for two players taking turns.
4. Play:
   - `WASD` moves.
   - `IJKL` aims and fires (Space also fires).
   - `P` pauses and `Esc` quits.

The factory settings reproduce the arcade as it shipped. To change the number of lives, the difficulty or the secrets and bugs, open the [settings](docs/settings.md) with `F5`.

A ROM image is not needed to play.

## About the author

I reverse engineered Robotron: 2084. I have read its code routine by routine, so I
know more than most about how the game works internally: how each enemy chooses its
target, how the wave tables and timers drive the difficulty, and the behaviours the
game never explains to the player. These include its secrets, such as **Bozo mode**,
a hidden mercy that slows the early waves for a player who is losing lives. The
original bugs are also kept, and each can be switched off from the settings.

This repository is that study turned into a playable game.

## Menus and settings

| Page | What it covers |
|---|---|
| [Main menu](docs/main-menu.md) | Starting a game, the player modes, the game style and the other menu rows |
| [Settings](docs/settings.md) | Every adjustable setting, with a page for each one explaining what it does |

The settings that reproduce arcade secrets and bugs have their own pages:
[Bozo mode](docs/settings/bozo-mode.md),
[Brains chase Mikey bug](docs/settings/brains-chase-mikey-bug.md) and
[Tank shell bug](docs/settings/tank-shell-bug.md).

## Technical details

How the port is built, how it was checked against the original, the decode log and
the repository layout are on a separate page:
[Technical details](docs/technical-details.md).

## Controls

**Controls:** `WASD` move · `IJKL` aim and fire (8-way; Space also fires) ·
`F1` / `F2` / `F3` start a one player, two player alternate or two player simultaneous game ·
`P` pauses (port-only; the arcade has no pause) · `F11` cycles the window scale · `Esc` quits.
Controls can be redefined from the developer tools and are stored in
`%LocalAppData%\Robotron2084\controls.ini`.

**Screens and function keys:**

- The [main menu](docs/main-menu.md) starts a game and chooses the game style.
- `F5` opens the [settings](docs/settings.md), which are saved to `%LocalAppData%\Robotron2084\settings.ini`.
- `F4` sound test and `F6` speech test play every sound or speech line one at a time
  (`Enter`, `Space`, `Right` or `Down` advance, `Left` or `Up` go back, `A` is auto-up).
- `F10` opens the developer tools: the attract screens, `DEFINE INPUTS`, `GAME ADJUSTMENT`,
  `SOUND TEST` and `SPEECH TEST`.
- `F1` to `F6` work from every attract screen, and `F10` from every screen.
- **Define inputs:** every MOVE/SHOOT direction of both players has its own line. `Enter` arms a line
  and the next key, pad button or stick push becomes it; `Del` clears it, `R` restores the factory
  scheme and `F10` saves and returns.

Keys for jumping around the attract sequence are listed in the
[technical details](docs/technical-details.md#attract-dev-keys).

## Credits and provenance

**Robotron: 2084** was designed by Eugene Jarvis and Larry DeMar (Vid Kidz) and
released by **Williams Electronics in 1982**. All of the art, the tables and the
behaviour decoded here are theirs. This repository is a non-commercial
reverse-engineering study and is not affiliated with or endorsed by the rights
holders. The ROM image and the original source listings are **not** distributed here;
you need your own copy of the ROM to regenerate assets.

The reverse engineering and the tooling are the author's: the hardware research; the
**sprite editor** (`WmsGfxSpriteEditor`), whose `RobotronBlueLabelSpriteRepository` is
the definitive name, offset and size list for every sprite and whose
`RobotronPaletteService` is the palette guide (following seanriddle.com's byte-to-RGB
conversion); the **annotated 6809 disassembly** (`asm/robomame.asm`) that ships in this
repository; and the playtesting that decided what the original really does.
