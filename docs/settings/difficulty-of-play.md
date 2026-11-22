# DIFFICULTY OF PLAY

[Settings](../settings.md)

Makes the waves easier or harder by moving the speeds and delays that the wave table specifies.

| | |
|---|---|
| **Values** | 0 (extra liberal) to 10 (extra conservative) |
| **Factory value** | 5 ("RECOMMENDED") |
| **Origin** | The arcade's own row (`difficulty_of_play`, CMOS `$CC14`) |

## What it changes

Twelve values are adjusted: the grunts' move delay and speed floor, the number of enforcers per spheroid and tanks per quark, how often enforcers and tanks fire, the spheroids' and quarks' drop delays, the hulks' beat interval, how often brains fire and how fast they beat, the tank shells' speed, and the quarks' top speed.

At **5** nothing changes: the wave table is used as it is. Above or below 5, each value is moved by an amount that grows with the distance from 5, then limited to a minimum and a maximum. The calculation follows the original routine step for step, including its rounding, so a given setting produces the same numbers as the arcade ROM.

## An easy setting is not kept for strong players

On the original, a setting below 5 is raised to 5 once a player reaches wave 14, or reaches wave 5 with three or more lives. This prevents a low setting from carrying a strong player through the later waves. The game reproduces this.

## Interaction with BOZO MODE

[BOZO MODE](bozo-mode.md) is applied first and the difficulty adjustment second, as on the arcade.
