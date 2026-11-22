# Hud Review

Scope: `src/Robotron2084/Hud` (excluding `Hud/Models`). The review emphasizes state/UI synchronization, layout, nullability, and Update/timing correctness.

## Findings

### 1. Initials timeout does not advance the cursor or restart a returned letter's deadline
**File:** `src/Robotron2084/Hud/InitialsEditor.cs`

`AdvanceTimeout()` decrements `_lettersLeft` and can complete the entry, but never increments `LetterIndex` or calls `SeedCell()` as successive letter deadlines expire. It therefore leaves the timeout's current cursor on the same cell even though the cell has become blank and the next letter should own the next timeout. Rub-out also increments `_lettersLeft` but does not reset `_timeoutClockUnits`, so a reused letter can receive less than the documented 640-frame deadline.

This can make late input, cursor rendering, and the internal "letters left" count diverge. It is also inconsistent with the class comments, which describe each remaining letter as moving the entry forward and rebasing the deadline.

**Impact:** state/UI mismatch and possible incorrect entry duration after inactivity or rub-out.

### 2. Typematic and cyclic input share unprotected cell access
**File:** `src/Robotron2084/Hud/InitialsEditor.cs`

`Cycle()` and `Commit()` index `_initials[LetterIndex]` without a bounds guard. Normal transitions keep `LetterIndex` between 0 and `LetterCount - 1` until completion, and timeout does not advance the index, but timeout/rub-out edge ordering is state-sensitive. `RubOut()` also decrements `LetterIndex` before there is a local guard on it.

While ordinary tests cover the intended paths, the invariant is implicit rather than enforced. The public API does not expose a way to stop timing methods once complete, although `Tick()` does guard `IsComplete`.

**Impact:** future state changes or a timeout/rub ordering bug can turn this implicit invariant into an `IndexOutOfRangeException`.

### 3. Settings and definitions menus assume the caller has kept the cursor valid
**Files:** `src/Robotron2084/Hud/SettingsMenu.cs`, `src/Robotron2084/Hud/DefineInputsMenu.cs`

Both menus keep `Line` as a private-state integer and expose public `GetLabel(int)`, `GetValue(...)`, `GetAction(int)`, and similar methods that accept arbitrary integers. Several methods have broad switches with `default`/subrange branches:

- `SettingsMenu.Change()` treats every line not explicitly handled as an action line and writes `_armedLines[Line]`. An invalid `Line` from future callers throws via array indexing only if out of range, while `GetLabel()`/`GetValue()` throw or index array elements inconsistently.
- `DefineInputsMenu.Assign()`/`ClearHighlighted()` call `SetLine()` for `Line`; `SetLine()` also dereferences `GetAction(line)!.Value` for non-pause, non-action pagespace. `Line` can only become a spacer if internal navigation changes incorrectly, since the navigation currently skips them.

**Impact:** brittle internal APIs; malformed state is not rejected at an early boundary and may become a UI-only `NullReferenceException` or out-of-range failure.

### 4. Pause edge state swallows the press that exits a state, but not a press already active on entry
**File:** `src/Robotron2084/Hud/PauseToggle.cs`

`Reset()` deliberately sets `_wasHeld = true`, then `Tick(false)` releases the latch. This is correct when leaving a game with a button still held. It prevents an accidental immediate re-pause.

However, the model has no constructor/state-reset distinction: if a newly constructed toggle is created while pause is already held, `_wasHeld` is false and the first tick toggles paused immediately. If the caller intended all held-at-entry cases to be swallowed, only `Reset()` provides that behavior.

**Impact:** state initialization depends on construction versus `Reset()` call order; holding pause at the first tick can differ depending on caller lifecycle.

### 5. High-score frame and print clocks are split, so no shared "frame committed" barrier exists
**Files:** `src/Robotron2084/Hud/HighScoreFrameAnimation.cs`, `src/Robotron2084/Hud/HighScorePrintSequence.cs`

The frame animation advances by two strokes per arcade frame. The print sequence starts immediately on `frameFinished`, and its first `Advance()` prints one group in the same Update that the frame reports finished. The state then draws that frame once:

- `_highScorePrintSequence.Tick(...)`
- `Draw()` later reads the updated counters

That is synchronized by the sequence API, but it is not a single snapshot: if `Tick()` is ever called more than once before `Draw()` or in a laggy loop, `TodayRows`/`AllTimeRows` can advance multiple groups and be visible at once rather than once per port frame.

**Impact:** timing correctness depends on each state being updated exactly once per MonoGame frame. The clock accumulators do not themselves enforce or detect that invariant.

### 6. High-score frame frontiers briefly disagree with drawn ink
**File:** `src/Robotron2084/Hud/HighScoreFrameAnimation.cs`

The frame starts with `_strokesDrawn = 2`, but `GetDrawnStroke()` reports `_strokesDrawn - 1`, so the initial public frontier is stroke 1 as documented. On the growing-to-erasing transition, `Advance(2)` sets `_isErasing = true` while `_strokesDrawn = 2`, then increments it for the same pair. `GetErasedStroke()` therefore begins at 1.

The state also validates finishedness through `_strokesDrawn >= 49`, but `_strokesDrawn` is overloaded as a count for growing and an erase frontier for erasing. It works for current constants but is harder to prove and makes one-off errors likely if constants or pass lengths change.

**Impact:** layout constants are tightly coupled and a pass-length change could make the band contain too few or too many strokes.

### 7. High-score row layout has a 10-row and 36-row capacity split that callers must maintain
**Files:** `src/Robotron2084/Hud/HighScoreTableLayout.cs`, `src/Robotron2084/Hud/HighScorePrintSequence.cs`

`TodayRows = 10`, `AllTimeRows = 36`, and rank/position calculations are independent constants in the layout class and sequence class. `DrawTodayList()` and `DrawAllTimeList()` also apply separate bounds:

- today: `rank <= 10 && rank <= entries.Count && rank <= printedRows`
- all-time: `rank <= 37 && rank - 2 < entries.Count && rank - 1 <= printedRows`

The all-time rank 1 is deliberately the top entry and rank 2 begins the list. This is consistent for current values, but the print sequence's counts are not validated against layout capacity. A larger `todayCount` or `allTimeCount` is clamped by `Math.Min` to layout-equivalent counts only because callers pass current table capacities.

**Impact:** future table or layout growth can silently create incomplete or mismatched print/draw windows.

### 8. HUD text and score layout have no clipping or null-surface checks
**Files:** `src/Robotron2084/Hud/ArcadeHud.cs`, `src/Robotron2084/Hud/ScoreFormatter.cs`

`DrawCentredLargeText()`, `DrawMessageText()`, `DrawScoresAndLives()`, and `DrawWaveMessage()` accept non-null strings/textures but do not guard unknown glyphs or oversized text. Unknown characters are skipped in the centering width calculation and in rendering inconsistently only if `ArcadeText` behavior differs.

`GetScoresAndLivesY()` can be negative. The caller and comments assume that is outside the canvas, but this is implicit. `LayOutGlyphs()` can also accept any origin and does not ensure the resulting right edge is within the canvas.

**Impact:** layout correctness is caller-dependent and there is no central bounds/clip contract.

## Positive synchronization observations

- `PlayingState` draws `gameSession` score/lives directly from the session, and the playfield mutates the same slot state, so the HUD is naturally state-driven rather than cached.
- `SettingsState` persists setting mutations immediately and resets the high-score store only after a confirmed `YES` activation.
- `DefineInputsState` separates idle navigation/capture and saves immediately after a successful assignment.
- `HighScoreTableState` polls input every tick so edge state remains live while switch checks are held off by the ROM-style hold.
- `InitialsEntryState` submits initials only after `InitialsEditor.IsComplete`, and the three `LetterCount` cells are checked at every draw.
