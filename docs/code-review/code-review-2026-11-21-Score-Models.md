# Score Models Review

## Scope

Reviewed `src/Robotron2084/Score/Models/FinalScore.cs`.

## Findings

### 1. FinalScore has no constructor invariants

`FinalScore` is a positional `record struct`, so it permits:

- `PlayerNumber` outside 1 or 2;
- negative `Score`;
- any `int` value outside the score model's intended range.

The documentation explicitly says `PlayerNumber` is 1 or 2 and `Score` is the player's score at end
of game, but the type does not enforce those constraints.

A record struct is suitable for a plain DTO, but if callers rely on these values being valid, add
an explicit constructor that validates:

- `PlayerNumber == 1 || PlayerNumber == 2`;
- `Score >= 0`.

### 2. Cross-model overflow/consistency is not enforced

`FinalScore.Score` is `int`, matching `ScoreBoard.Score`, so normal overflow concerns are shared.
The model should not accept a negative or wrapped score that originated from an overflowed
`ScoreBoard`.

### 3. Ordering and equality semantics are implicit

The record struct provides value equality and includes both fields, but no ordering or de-duplicated
identity exists. If high-score insertion compares or sorts `FinalScore` values, that behavior should
be explicit in the model or comparison service rather than inferred from field declarations.

## Positive observations

- The type is simple and immutable.
- The docs preserve the two-player arcade ordering and port sorting intent.
- It has no mutable reference fields or hidden state.
