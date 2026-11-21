# Score Review

## Scope

Reviewed `src/Robotron2084/Score/ScoreBoard.cs` and
`src/Robotron2084/Score/ScoreValues.cs`.

## Findings

### 1. ScoreBoard does not protect int overflow

`Score += points` is unchecked. A sufficiently large positive `points` can wrap the score negative,
violating the nonnegative-score invariant. Conversely, `Score` can be populated only by
nonnegative `SetScore`, but `Add` can bypass that guard.

Likewise, `_nextExtraLifeThreshold += _extraLifeEveryPoints` and
`(score / _extraLifeEveryPoints + 1) * _extraLifeEveryPoints` can overflow near `int.MaxValue`.
The overflow can produce a wrong threshold or a negative score.

Use checked arithmetic or saturating/`long` arithmetic, and define behavior for a score above
`int.MaxValue` (the model currently cannot represent it). Reject negative `points` if a score event
is required to be positive.

### 2. Add can award only one life for crossing multiple thresholds

`Add` checks the threshold once and increments `_nextExtraLifeThreshold` once. If one call moves
the score across two or more extra-life multiples, the method returns only one `true`.

The API documentation says "A spare life is earned each time" and `Add` reports the life, so this
must award/report all thresholds crossed or a caller must receive an explicit count. The current
`bool` contract cannot represent two or more awards.

Example: at 0 with a 20,000-point interval, a one-call 50,000-point add crosses 20,000 and
40,000 but returns `true` only once.

### 3. Negative point additions are not rejected

`Add(int points)` permits negative values, although no code in this module is intended to subtract
points and the class description says points are added. This can decrease `Score` below the most
recent threshold and break the invariant that score never decreases.

Add should reject negative points with `ArgumentOutOfRangeException`, or document and test
subtractions as a supported operation.

### 4. Extra-life setting is not validated in the constructor

`extraLifeEveryPoints` is accepted without checks:

- `0` explicitly disables extra lives.
- negative values disable thresholds in `SetScore` because it checks `> 0`, but leave a potentially
  nonsensical field value.
- `_nextExtraLifeThreshold` never uses it for negative values.

If negative settings are invalid, reject them. If negative is equivalent to disabled, normalize or
document that behavior.

### 5. SetScore threshold formula can overflow

For positive `_extraLifeEveryPoints`, `SetScore` computes
`(score / extraLifeEveryPoints + 1) * extraLifeEveryPoints`.

At high scores this can overflow even though `score` itself is valid. It can also produce a threshold
that is not a multiple, or `int.MaxValue` values around wraparound.

The overflow-safe next-threshold calculation should use `long`, or return `int.MaxValue` if no
award is representable.

### 6. RescueBonus is currently stable but has no explicit input invariant

`Math.Clamp(rescuesThisLife, 1, RescueBonusMaxCount)` makes 0 and negative values produce the
first-rescue bonus of 1000. If negative rescue counts are prohibited, this silent clamping is
concealing invalid caller state. If 0 is a valid alias for "this is the first rescue," document that.

The multiplication is currently safe (`5 * 1000 = 5000`).

## Positive observations

- `SetScore` rejects negative scores.
- Existing tests cover splitting awards across fields, threshold skips, no-extra-life mode, and
  exact multiples.
- `Electrode = 0` and all published kill values are nonnegative.
- Rescue bonuses stop at the fifth rescue.
