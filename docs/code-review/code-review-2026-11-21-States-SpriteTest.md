# Review: States/SpriteTest

## Scope

Reviewed C# files directly under `src/Robotron2084/States/SpriteTest`:
`SpriteTestAnimator`, `SpriteTestEntries`, and `SpriteTestWalkOnRun`.
Nested `SpriteTest/Models` is reviewed separately.

## Findings

### Medium — `SpriteTestEntries.Create` has several construction assumptions

- **Evidence:** `SpriteTestEntries.Create` assumes:
  - every steerable entry is generated for all `Direction8` values;
  - `GetFirstWaveWith` finds a positive count;
  - `GetFirstWaveWithElectrodeVariant` finds a variant within the first
    ten-wave palette cycle;
  - required texture arrays contain the slices requested.
- These assumptions are enforced only indirectly. Invalid tuning/table changes
  can result in an empty entries list, a missing cycle, or an array
  `IndexOutOfRangeException` before `SpriteTestState` can render.

**Recommendation:** validate tuning/table invariants once while building the
list and throw a clear `InvalidOperationException` naming the missing texture,
wave, or variant rather than allowing an incidental array exception.

### Low — Animator keeps the previous frame when a cycle is unavailable

- **Evidence:** `SpriteTestAnimator.Tick` returns when
  `entry.GetCycle(heldDirection)` is null.
- That is documented as “stay on the frame showing,” but a cycle change from
  a previous direction to null occurs on the same animator and does not reset
  the frame or timing fields.
- The resulting state is valid only if the entry intentionally remains on the
  current frame. `_entry.GetCycle(null)` is used by the constructor, but several
  entries use a null cycle to mean freeze.

**Recommendation:** retain this behavior explicitly in tests and make it a
named entry model contract. Avoid null cycles with a mixed/ambiguous meaning
unless freeze is deliberately intended.

### Low — Random hold and timing can drift through slow frames

- `SpriteTestAnimator.Tick` subtracts one port tick and advances at most one
  frame per tick. The selected frame hold is in clock units.
- `_clockUnitsLeft` can become negative after an unusually slow frame, but the
  next update immediately checks and catches up; no invalid frame index can
  result because modulo is used.
- Random hold uses `Random.Next(RandomHoldMax)`, and constructors reject empty
  cycles.

No immediate timing defect is present.

### Low — Walk-on step counts depend on frame-change count

- `SpriteTestWalkOnRun.Tick` snapshots `FrameChangeCount` at walk start and
  moves once for every later frame change when no fixed step interval exists.
- If a sprite's cycle remains on the same frame or changes cycle without
  moving frame, no step is taken. This is consistent with the model contract,
  but can leave a walk-on indefinitely for an entry without frame changes.
- Fixed-interval walks may take multiple steps in one tick if
  `_clockUnitsToNextStep` falls far behind. The while loop is bounded only by
  clock/time, and `TakeStep` wraps step indices.

**Recommendation:** add tests for entries where a walk-on has no frame-change
steps or unusually large elapsed ticks, and ensure the run reaches its home
position in a bounded way.

### Update/Render

- `SpriteTestState.Update` resets idle and cancels a walk when any keyboard,
  pad, stick, or trigger is touched.
- Navigation wraps correctly and reconstructs the animator on every entry
  change.
- `DrawSprite` and `GetHomeBounds` use the current texture's dimensions and
  scale; walk-on uses the adjusted left edge and ground rise.
- F10 is handled by the shell in `RobotronGame.HandleDeveloperToolsKey`, not
  by `SpriteTestState` itself. The shell currently includes this state among
  states that should receive F10 normally, so the result is developer tools.

No invalid index or transition was found for normal navigation. The state must
remain non-empty and require valid entries for construction.

## Summary

The sprite-test implementation is structurally consistent. Main risks are
upstream tuning/texture invariants and whether walk-on timing eventually
stops for entries with frame-change-only steps. No source changes were made.
