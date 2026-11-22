# BOZO MODE

[Settings](../settings.md)

A mercy the arcade gives a player who is losing lives on the first few waves. The first release of the game did not have it; the second added it because new players found the game too hard.

| | |
|---|---|
| **Values** | ON / OFF |
| **Factory value** | ON |
| **Origin** | The arcade's behaviour (routine `PLRES` in `RRG23.ASM`, table `BOZOTB`, ROM `$2B26`). The switch is port only. |

## When it applies

Each time a wave is loaded for a player:

- On **waves 1 to 4**, a player with **no spare lives** receives it.
- On **waves 1 and 2**, a player who has lost a life but still has spares receives it.

## What it does

The wave's grunts, spheroids and enforcers are slowed to values from a four-row table. Each later wave uses a faster row:

| Wave | Spheroid drop delay | Enforcer fire delay | Grunt move delay | Grunt speed floor |
|---|---|---|---|---|
| 1 | 38 | 96 | 30 | 15 |
| 2 | 38 | 96 | 25 | 12 |
| 3 | 36 | 48 | 20 | 10 |
| 4 | 30 | 30 | 15 | 7 |

A larger number is a longer wait, so the enemies move and fire less often.

## Notes

- The mercy is not announced; the player is not told it is happening.
- With the setting OFF it is never given, and the wave table is used unmodified.
- It is applied before [DIFFICULTY OF PLAY](difficulty-of-play.md).
- The "spare lives" comparison uses [TURNS PER PLAYER](turns-per-player.md).
