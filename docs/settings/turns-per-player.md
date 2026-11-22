# TURNS PER PLAYER

[Settings](../settings.md)

The number of lives each player starts a game with.

| | |
|---|---|
| **Values** | 1 to 20 |
| **Factory value** | 3 ("RECOMMENDED") |
| **Origin** | The arcade's own row (`turns_per_player`, CMOS `$CC02`) |

The page prints the arcade's description beside some values: HIGH VOLUME ARCADES (2), RECOMMENDED (3) and FOR WEAKER PLAYERS (4).

This setting also affects [BOZO MODE](bozo-mode.md): the original compares a player's spare lives with the number they started with when deciding whether the player is behind.
