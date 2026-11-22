# EXTRA MAN EVERY

[Settings](../settings.md)

The score a player must reach to earn a spare life, and then again each time the score passes another multiple of it.

| | |
|---|---|
| **Values** | no extra men, 20000, 25000, 30000, 50000 |
| **Factory value** | 25000 ("RECOMMENDED") |
| **Origin** | The arcade's own row (`extra_man_every`, CMOS `$CC00`) |

The five values are the five stops the original ROM allows. `Left` and `Right` move between them and stop at either end. Under the value the page prints the arcade's own description: NO EXTRA MEN, LIBERAL, RECOMMENDED, CONSERVATIVE or EXTRA CONSERVATIVE.

Choosing "no extra men" means a player has only the lives given by [TURNS PER PLAYER](turns-per-player.md) for the whole game.
