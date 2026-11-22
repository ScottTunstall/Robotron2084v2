# Code review ledger — 1 November 2026 (tests: `tests/`)

Companion to `code review 1-NOV-2026.md` (main game). Standard: `docs/coding-standards.md`; TEST rules apply
with the general rules on top (TEST-6). Line numbers from commit `81f31f7`. `tools/` is out of scope by
request (it does carry the 886-line `SpriteExtractor/Program.cs`, three >50-line methods, and one nested type
if the author later wants that reviewed).

---

## 1. Blocker

### t-B-01 — [Blocker] TEST-2 — the shared `PlayFieldBuilder` defaults to an invincible player
`tests/Robotron2084.Tests/PlayFieldBuilder.cs:36` — `private bool _playerInvincible = true;` flows into the
`PlayField` constructor at `:159`. Every test built without `.WithPlayerInvincible(false)` silently tests an
invincible player — collision, death and wave tests can pass while the shipped game's behaviour is never
exercised. This is the test-side twin of main-ledger **B-01/B-02** (which is worse: the *production* default is
also `true`). Fix together: flip this default to `false`, then have the tests that rely on the aid call
`.WithPlayerInvincible(true)` explicitly (e.g. `WaveStartSequenceTests.cs:45` already parameterises it;
`PlayFieldCollisionTests.cs:311,324` defaults its own helper to `true` — make that an explicit choice per
test, not a helper default).

### t-B-02 — [Blocker, shared with main ledger] TEST-2 — a test asserts the global invincibility aid is ON
`tests/Robotron2084.Tests/Entities/PlayerInvincibilityTests.cs:21` — `Assert.True(PlayerTuning.PlayerInvincibleForTesting)`
("the flag must be on for this test to mean anything"). It pins the shipped-game blocker B-02 in place: fixing
B-02 breaks this test. When B-02 is fixed, rewrite this test to enable the aid through the constructor/builder
and assert the *behaviour*, not the global flag.

---

## 2. Major

### t-M-01 — [Major] STR-3 — nested types (one type per file, no nesting)
Pull each helper into its own file in the same folder (tests included — TEST-6):
- `Entities/PlayerAnimationAndAimTests.cs:159` — `public sealed class PhaseInput`.
- `Level/PlayFieldBrainProgMissileTests.cs:827` — `public sealed class PhaseInput` — **a near-duplicate of the
  one above** (same name, same shape): extract once to a shared helpers file and delete both (CON-1).
- `Entities/StripEffectTests.cs:570` — `private sealed class FakeDead`.
- `Input/DemoPlayerInputSourceTests.cs:170` — `private sealed class MutableStick` (also embedded in
  `PhaseInput` — one shared `MutableStick` helper serves both).
- `Level/PixelCollisionTests.cs:92` — `private sealed class StubPixelCollision`.
- `Palette/PaletteAnimatorTests.cs:227` — `private sealed class MaxRecordingRandom`.

### t-M-02 — [Major] STR-1a — test classes over the 500-line hard cap
- `tests/Robotron2084.Tests/Level/PlayFieldBrainProgMissileTests.cs` — 836 lines.
- `tests/Robotron2084.Tests/Entities/StripEffectTests.cs` — 618 lines.
Split by behaviour (TEST-5's one-thing files), not just by line count.

### t-M-03 — [Major] STR-2a — test method over 50 non-blank lines
- `tests/Robotron2084.Tests/Entities/HulkAnimationTests.cs:33` — method runs to line 106 (~67 non-blank lines).
Break the Arrange block into named helper calls (TEST-3's naming discipline works well here).

### t-M-04 — [Major] NUM-2 / TIME-1 — arcade clock hand-rolled as `5` and `6` in tests
Tests must state arcade time through the same helpers the game uses (`ArcadeClock.UnitsPerArcadeFrame`,
`ToPortTicks`); a test computing the conversion by hand can drift from the code it tests and both be wrong
together.
- `Entities/QuarkTankBehaviourTests.cs:143` — `portFrames = frames * 6 / 5;` (helper
  `ArcadeFramesToPortFrames`) — use `ArcadeClock.ToPortTicks(frames)`.
- `Hud/HighScorePrintSequenceTests.cs:114` — `ToPortTicks(frames) => (frames * 6 + 4) / 5;` — same.
- `Hud/InitialsEntryModelTests.cs:258` — `TimeSpan.FromSeconds(ticks * 6.0 / 5.0)` — same (rounded variant).
- `Palette/PresentationPagePaletteTests.cs:17` — `(ticks * 6) / 5` — same.
- `Graphics/PlayerLaserAnimationFrameTests.cs:17` — `private const int N = 6;` used as the arcade-clock ratio
  at `:43,48` (`_frame * N` = arcade frames × ticks-per-frame). Two findings: NUM-2 (the clock as a bare `6`)
  and NAM-19/NAM-5 (the name `N` hides what it counts) — replace with `ArcadeClock.UnitsPerArcadeFrame`
  throughout.
- `Entities/SparkTests.cs:178` — `for (var move = 0; move < 6; move++)` — verify: if `6` is one arcade frame's
  worth of moves, it is NUM-2; if it is just "six moves of steering", rename the local to say so (NAM-19).
- `Palette/PaletteAnimatorTests.cs:93` — comment "one palette rotation every 2 arcade frames (6 ticks per
  arcade frame, …)" — CMT-4: the numbers live in `ArcadeClock`; reference the helper name instead.

### t-M-05 — [Major] CON-1 / TEST-5 — duplicated fixtures across test files
- `PhaseInput` in two files (see t-M-01) and `MutableStick` reimplementing part of it — one shared helpers
  file (`tests/Robotron2084.Tests/Support/` or similar) for `PhaseInput`, `MutableStick`,
  `MaxRecordingRandom`, `StubPixelCollision`, `FakeDead`.
- The `PlayField` construction block in `PlayFieldCollisionTests.cs:311-324` predates `PlayFieldBuilder` and
  hand-rolls the same wiring — switch it to the builder (TEST-5).

---

## 3. Minor

### t-m-01 — [Minor] NAM-2 — unit suffix on a test fixture field
`Entities/PlayerAnimationAndAimTests.cs:159` — `public int ArcadeTicks;` inside `PhaseInput` holds the *port*
tick count between arcade frames (values like 5/6 are compared against `ArcadeClock`-derived timing at
`:175`). If it counts port ticks it must be `…PortTicks` (NAM-2); if arcade ticks, its values and uses need a
second look. Verify against the use sites and rename.

### t-m-02 — [Minor] NAM-13/NAM-4 style in test helper names
`Level/PlayFieldCollisionTests.cs:311` — `CreateEmptyField(bool playerInvincibleForTesting = true)` — its
default duplicates the t-B-01 problem one layer down; drop the default when the builder default flips.

### t-m-03 — [Minor] CMT-4 — comments restating numbers that live in `ArcadeClock`/constants
- `Palette/PaletteAnimatorTests.cs:93` (also in t-M-04): "2 arcade frames (6 ticks per arcade frame, 8 ticks
  per rotation)".
- `Hud/HighScorePrintSequenceTests.cs:114`'s helper comment describes the 5/6 rounding — the formula is the
  finding, the comment inherits it.
Sweep test comments for `6 ticks`, `50 Hz`, `1/50` phrasing during the t-M-04 fix.

### t-m-04 — [Minor] TEST-3 — assertion messages
Spot checks looked disciplined (most asserts carry a message where the value could be confused). No
mechanical scan distinguishes "obvious" from confusable; do one read-through per file when touching it for
the findings above.

---

## 4. Notes

### t-N-01 — [Note] Follow-ups gated on the main ledger
- After main **B-01/B-02** and t-B-01 flip the invincibility defaults, run the full suite: tests that quietly
  depended on an invincible player will fail — each is itself a finding (a test that could not detect a death).
- `PlayerInvincibilityTests` needs the rewrite described in t-B-02.

### t-N-02 — [Note] Rules not audited this round (needs by-hand pass)
TEST-1 Given/When/Then paragraph structure per test (not mechanically checkable), TEST-3 per-assert message
quality, TEST-4 file-placement of a few oddments (`tests/…/AttractStateTests.cs` vs `States/` folder split —
spot-checked as plausible), STR-5 member order in test classes, CMT rules on test-file summaries.

### t-N-03 — [Note] Not findings
- `CruiseMissile_DragsARollingNineMarkTail` — "rolling" here means "trailing", the word's other meaning,
  which NAM-24 explicitly allows (unlike "cycling").
- Nested-type *helpers* are not TEST-2 "test hooks in production" — they live in tests, where they belong;
  they are flagged under STR-3 (file shape) only.
- `MaxRecordingRandom`, `StubPixelCollision`, `FakeDead` are legitimate test doubles (TEST-2's target is
  production hooks, not doubles).

---

## Summary
- **Blocker:** 2 (invincible-player default in the shared builder; a test asserting the global aid is on).
- **Major:** 5 entries (STR-3 ×6 nested types incl. one duplicate pair, STR-1a ×2, STR-2a ×1, NUM-2 clock
  hand-rolling ×6-7 sites, CON-1 fixture duplication).
- **Minor:** 4 entries. **Notes:** 3.
- Most important: **t-B-01/t-B-02** together with main-ledger **B-01/B-02** — the invincibility aid is on by
  default in both the shipped game and every default-built test field, so no current test can catch it.
