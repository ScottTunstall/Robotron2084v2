# Settings

[Back to the README](../README.md) · [Main menu](main-menu.md)

The settings page is the arcade cabinet's **GAME ADJUSTMENT** page in this game's own presentation. It is opened from the [main menu](main-menu.md) or with `F5`.

The arcade's rows that change how a game plays are included, in the arcade's order, with the arcade's own ranges and factory values. The coin and pricing rows are omitted because the game has no coin handling.

Four further rows are specific to this port. Three of them let you switch off a behaviour that the arcade always has. Those three are worth reading about if you are interested in how the original works.

## The settings

| Setting | Values | Factory value | Origin |
|---|---|---|---|
| [EXTRA MAN EVERY](settings/extra-man-every.md) | none, 20000, 25000, 30000, 50000 | 25000 | Arcade |
| [TURNS PER PLAYER](settings/turns-per-player.md) | 1 to 20 | 3 | Arcade |
| [DIFFICULTY OF PLAY](settings/difficulty-of-play.md) | 0 to 10 | 5 | Arcade |
| [ATTRACT MODE SOUND](settings/attract-mode-sound.md) | ON / OFF | OFF | Port only |
| [BOZO MODE](settings/bozo-mode.md) | ON / OFF | ON | Arcade behaviour, switchable |
| [BRAINS CHASE MIKEY BUG](settings/brains-chase-mikey-bug.md) | ON / OFF | ON | Arcade behaviour, switchable |
| [TANK SHELL BUG](settings/tank-shell-bug.md) | ON / OFF | ON | Arcade behaviour, switchable |
| [RESTORE FACTORY SETTINGS](settings/restore-factory-settings.md) | NO / YES | NO | Arcade |
| [HIGH SCORE TABLE RESET](settings/high-score-table-reset.md) | NO / YES | NO | Arcade |

## Controls

- `Up` / `Down` select a row.
- `Left` / `Right` change the value.
- `Enter` performs an action row (the last two) once it reads `YES`.
- `F10` saves and returns.

## Where settings are stored

Settings are saved to `%LocalAppData%\Robotron2084\settings.ini`. The file can be edited by hand and is read when the game starts. Control definitions are kept separately in `controls.ini`.

## A note on accuracy

The factory values reproduce the arcade as it shipped. Changing a setting moves the game away from the original, with one qualification: a value chosen within the arcade's own range (for example DIFFICULTY OF PLAY) behaves as the original ROM would behave at that value.
