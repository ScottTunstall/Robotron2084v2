# Hud Models Review

Scope: `src/Robotron2084/Hud/Models`. These types are small, but their invariants and nullability directly affect state/UI synchronization.

## Findings

### 1. `ScoreDigit.Suppressed` does not itself validate ROM/port digit invariants
**File:** `src/Robotron2084/Hud/Models/ScoreDigit.cs`

The record struct permits `Value` outside 0-9 and `Suppressed` combinations that no formatter should produce. It is publicly constructible, with no constructor guard or invariant documentation beyond parameter comments.

`ArcadeHud`/`ScoreFormatter` only create instances through `GetDigits()`, so current callers are safe. However, tests or future callers can create invalid glyph data.

**Impact:** low current risk, but this model does not fail fast at its boundary.

### 2. `ScoreGlyph.Digit` has no 0-9 validation
**File:** `src/Robotron2084/Hud/Models/ScoreGlyph.cs`

Like `ScoreDigit`, `ScoreGlyph` is a public record struct that can represent any integer and any X coordinate. `LayOutGlyphs()` generates valid values, but its return contract is not enforced by the type.

**Impact:** invalid glyph indexes could be sent to texture/glyph lookup code in future use.

### 3. The entry phase enum has no transition-owned invariant
**File:** `src/Robotron2084/Hud/Models/InitialsEntryPhase.cs`

`InitialsEntryPhase` is internal and its comments describe the waiting/cycling/typematic states. Nothing in the enum or editor prevents illegal transition combinations (for example, `Typing` with no letters left). The editor is the only owner and currently guards completion in `Tick()`, but phase correctness remains implicit.

**Impact:** state/UI synchronization can become fragile if new initials-entry branches are added.

### 4. High-score print phase is internal and has no forward-only invariant
**File:** `src/Robotron2084/Hud/Models/HighScorePrintPhase.cs`

The enum correctly names the print order, but permits arbitrary assignment if future code bypasses `Advance()`. `_phase` is private and only set in sequence order today, so there is no runtime or compile-time enforcement that a transition is forward-only.

**Impact:** low current risk; future print behavior could regress to duplicate or out-of-order page phases.

### 5. `SettingsAction` conflates “none” with unarmed and no action
**File:** `src/Robotron2084/Hud/Models/SettingsAction.cs`

`SettingsAction.None` means both "nothing happened" and "the action row was not armed." It does not distinguish a value-row Enter, an action row set to NO, or an invalid call. `SettingsState.Activate()` only needs to perform the two real actions, so it is sufficient today.

**Impact:** future UI code cannot tell *why* activation produced no action, so feedback or event logging would require changing the model.

## Nullability and reference observations

- None of these model types use nullable reference fields; the record structs are small value types.
- `SettingsAction` is a plain enum and has no invalid/unknown sentinel beyond `None`.
- `InitialsEntryPhase` and `HighScorePrintPhase` are internal, reducing misuse outside the HUD package.

## Positive observations

- `ScoreGlyph` and `ScoreDigit` are readonly record structs, preventing accidental mutation after layout.
- `SettingsAction` is a small, closed set and is handled exhaustively enough for the settings state’s current branches.
- Both phase enums are internal implementation details and match the state machines that consume them.
