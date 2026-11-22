# BRAINS CHASE MIKEY BUG

[Settings](../settings.md)

Chooses whether the arcade's brain targeting behaviour is kept.

| | |
|---|---|
| **Values** | ON / OFF |
| **Factory value** | ON |
| **Origin** | The arcade's behaviour (routine `BRNSTV` in `RRB10.ASM`). The switch is port only. |

## The behaviour

A brain is meant to hunt the human nearest to it. When a wave starts, the original routine that finds the nearest human (`GETHTG`) runs before the family has been created. The only family member it can find is the first Mikey, so **every brain begins the wave heading for Mikey**, wherever it is placed.

## The setting

- **ON** reproduces the arcade. Every brain starts by chasing the first Mikey.
- **OFF** gives each brain the human nearest to it from the start, which is what the routine appears to intend.

Waves without brains are unaffected.
