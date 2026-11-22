# Robotron: 2084 — a from-source port in MonoGame

<img width="1916" height="1200" alt="title page 1" src="https://github.com/user-attachments/assets/ad77ae6d-e5de-407f-a417-c2a1043a598c" />

## About this project

Next to GORF, Robotron: 2084 is my favourite arcade game of all time.
I reverse engineered Robotron: 2084 back in 2014. Or most of it anyway. That work
gave me a detailed understanding of how the game runs internally. I know secrets.

Anyhow, thanks to the advent of AI coding, I thought I'd create something I always dreamt of: my own version.

I hope you enjoy.

Oh yes: in case you think this is AI slop, you might want to check my [coding standards](docs/coding-standards) and my code review documents. I keep hands on in the code and review it myself. I **aggressively refactor** the code I deem unacceptable.

Anybody who knows me IRL knows I won't tolerate substandard code.


## Quick start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Build and run the game from the repository folder:

   ```bash
   dotnet build Robotron2084.slnx
   ./src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe
   ```

3. On the [main menu](docs/main-menu.md), press `F1` or `Enter` to start a one player game, or `F2` for two players taking turns.
4. Play:
   - `WASD` moves.
   - `IJKL` aims and fires (Space also fires).  You can change the controls by using [Define Inputs
   - `P` pauses and `Esc` quits.

The factory settings reproduce the arcade as it shipped. To change the number of lives, the difficulty or the secrets and bugs, open the [settings](docs/settings.md) with `F5`.

## Menus and settings

| Page | What it covers |
|---|---|
| [Main menu](docs/main-menu.md) | Starting a game, the player modes, the game style and the other menu rows |
| [Define inputs](docs/define-inputs.md) | Choosing the keys, controller buttons and sticks for both players |
| [Settings](docs/settings.md) | Every adjustable setting, with a page for each one explaining what it does |

The settings that reproduce arcade secrets and bugs have their own pages:
[Bozo mode](docs/settings/bozo-mode.md),
[Brains chase Mikey bug](docs/settings/brains-chase-mikey-bug.md) and
[Tank shell bug](docs/settings/tank-shell-bug.md).

## Controls

**Controls:** `WASD` move · `IJKL` aim and fire (8-way; Space also fires) ·
`F1` / `F2` / `F3` start a one player, two player alternate or two player simultaneous game
(`1` and `2` also start a one or two player game) ·
`P` pauses (port-only; the arcade has no pause) · `F11` cycles the window scale · `Esc` quits.
The keys, buttons and sticks can be changed on the [Define inputs](docs/define-inputs.md) page,
which is a row on the [main menu](docs/main-menu.md). Your choices are stored in
`%LocalAppData%\Robotron2084\controls.ini`.

**Screens and function keys:**

- The [main menu](docs/main-menu.md) starts a game and chooses the game style.
- `F5` opens the [settings](docs/settings.md), which are saved to `%LocalAppData%\Robotron2084\settings.ini`.
- `F4` sound test and `F6` speech test play every sound or speech line one at a time
  (`Enter`, `Space`, `Right` or `Down` advance, `Left` or `Up` go back, `A` is auto-up).
- `F10` opens the developer tools: the attract screens, [`DEFINE INPUTS`](docs/define-inputs.md), `GAME ADJUSTMENT`,
  `SOUND TEST` and `SPEECH TEST`.
- `F1` to `F6` work from every attract screen, and `F10` from every screen.

Keys for jumping around the attract sequence are listed in the
[technical details](docs/technical/technical-details.md#attract-dev-keys).

## Technical details

How the port is built, how it was checked against the original, the decode log and
the repository layout are on a separate page:
[Technical details](docs/technical/technical-details.md).

## Credits

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
