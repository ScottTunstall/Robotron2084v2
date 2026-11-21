# Review: States/SpriteTest/Models

## Scope

Reviewed only C# files directly under
`src/Robotron2084/States/SpriteTest/Models`:
`SpriteTestCycle`, `SpriteTestDrawStyle`, `SpriteTestEntry`,
`SpriteTestFrame`, and `SpriteTestWalkOn`.

## Findings

### Medium — `SpriteTestCycle` rejects zero frames but not invalid holds

- `SpriteTestCycle` constructor rejects an empty frame list, preventing the
  primary index-out-of-range state.
- It does not validate frame hold values or `RandomHoldMax`. A frame with zero
  or negative `HoldClockUnits` may advance immediately or oscillate; a
  `RandomHoldMax` below 1 reaches `Random.Next(0)`.
- `SpriteTestAnimator` always calls `_cycle.Frames[FrameIndex]` and only advances
  modulo after construction, so frame-list invalidity is more important than
  hold values but both are mutable only through public constructors/models.

**Recommendation:** require every hold to be positive and
`RandomHoldMax >= 1`, or centralize validation in `SpriteTestAnimator.StartCycle`.

### Low — `SpriteTestEntry.GetCycle` is a nullable contract with no explicit freeze enum

- Null means freeze on the current frame by `SpriteTestAnimator.Tick`, while
  an entry constructor normally uses a concrete down/null fallback.
- Public callers can return a null cycle unexpectedly, and
  `SpriteTestAnimator` constructor will reject entries with neither a null-held
  cycle nor a down cycle.
- This makes freeze/still behavior implicit rather than represented by a named
  model state.

**Recommendation:** add an explicit `Unsteerable/Freeze` rule or require every
entry to return a concrete cycle, while keeping the still cycle as a one-frame
cycle.

### Low — `SpriteTestWalkOn` permits an empty step list

- `SpriteTestWalkOn.GetStepPortPixels` performs modulo by
  `StepPortPixels.Count`. An empty or null list yields
  `DivideByZeroException`/null dereference in a walk.
- Records are public and constructor positional parameters perform no validation.
- A negative or zero step is syntactically allowed, although negative may be
  intentional in some callers because this model is not consistently sign-based.

**Recommendation:** require at least one step and a non-negative interval/rise
configuration, unless negative steps are explicitly documented as supported
for entity-specific semantics.

### Low — `SpriteTestFrame.Texture` and draw style values are unchecked

- `SpriteTestFrame` can hold a null texture through external construction,
  although C# nullable analysis should make this visible.
- `SpriteTestDrawStyle` accepts invalid numeric enum casts. `SpriteTestState`
  handles unknown values by falling into `Normal` in its switch, which silently
  degrades an invalid state.

**Recommendation:** validate enum values and textures at construction; keep
unknown draw styles as explicit exceptions rather than a silent normal fallback.

## Lifecycle Notes

These types are data-only and are not lifecycle states. Their main role is to
define valid animation/walk data for `SpriteTestAnimator` and
`SpriteTestWalkOnRun`. Constructor validation is the safest boundary because
records expose immutable collections to consumers.

## Summary

The model classes are compact and mostly immutable. The missing validations are
the empty step list, invalid hold/random values, nullable freeze contract, and
unknown enum/draw values. No source files were changed.
