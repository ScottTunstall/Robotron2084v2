# TANK SHELL BUG

[Settings](../settings.md)

Chooses whether the arcade's tank shell counter is kept as it shipped.

| | |
|---|---|
| **Values** | ON / OFF |
| **Factory value** | ON |
| **Origin** | The arcade's behaviour (routine `SHELL` in `RRTK4.ASM`). The switch is port only. |

## The behaviour

A tank fires only while the wave's shell count is within a limit of 20. The count is meant to fall when a shell leaves the playfield. A shell that is shot down decreases it, but a shell that **fizzles out** after its short lifetime does not: the original routine has no decrement for that case.

Fizzled shells therefore stay on the count permanently. Once the limit is used up, **the tanks on that wave stop firing**. In this game the limit is reached after 21 shells have been fired.

## The setting

- **ON** reproduces the arcade. Fizzled shells remain counted.
- **OFF** counts only the shells currently on the playfield, so the tanks keep firing.

Waves without tanks are unaffected.
