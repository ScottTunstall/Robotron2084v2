# Review: Input/Models (non-recursive)

## Scope

Reviewed C# files directly contained in `src/Robotron2084/Input/Models`:
`PlayerInputState`, `InputSnapshot`, `InputBindingKind`, `InputBinding`,
`InputAction`, `ActionBinding`, and `ButtonPresses`. No nested source directories
were reviewed.

## Poll state and edge state

`PlayerInputState` is the immutable poll result consumed throughout the game.
The documented contract requires move and shoot components to be -1/0/1. The
primary constructor does not enforce that contract, however. Only
`PlayerControls`, `GamePadSticks`, and the demo source construct known-valid
vectors; arbitrary callers can use out-of-range `IntVector2` values.

`ButtonPresses` is only a result DTO for `ButtonEdgeDetector`, and
`AnyPressed` is a simple OR. It carries no polling history itself.

`InputSnapshot` captures one tick of keyboard and pad state. It is a record
struct, so it is suitable for previous/current capture comparisons and live
hardware reads. `GetPad` silently maps every index except 1 to pad 1; callers
outside `ControlCapture` could pass -1 or 2 and unexpectedly receive pad 1.
An explicit index guard would make this API safer.

## Mapping and bindings

`InputAction` defines the eight move/shoot actions. `InputActions.All` supplies
the canonical UI order. `InputBindingKind` distinguishes unbound, key, button,
left-stick, and right-stick bindings.

`InputBinding` encodes keyboard/pad device and code in one record. Stick
direction is stored by a packed screen-space code. `CreateStick` clamps X/Y to
-1/0/1 and rejects (0,0). It does not validate the gamepad index; an index other
than 1 is later accepted as pad 1 by `IsHeld`, while `CreateButton` likewise
has no explicit 0/1 invariant.

Parsing is intentionally vocabulary-compatible with display names and legacy
dash/colon abbreviations. `TryParse` has deterministic failure for unknown keys,
buttons, pads, and stick directions, and `None` accepts empty, "-", and "NONE".
The display/parse pair is well aligned for the tested values.

Known parsing/mapping risks:

- `TryParseGamePad` accepts pad indices only because it derives `P1`/`P2`
  before the parser. It rejects P3 through `TryParse` test coverage; direct
  construction of an invalid `InputBindingKind`/code is still possible.
- `GetDirectionName` is an internal invariant lookup. A binding constructed
  with an unknown direction code would throw rather than return a neutral name.
- `GetDisplayName` casts arbitrary numeric `Code` values to `Keys` or `Buttons`
  and enum `ToString` may produce an unexpected or unrecognized name.
- `Enum.TryParse` is not paired with `Enum.IsDefined` in the public display
  pipeline. Numeric strings may also be interpreted by Enum parsing.

`ActionBinding` provides one keyboard slot and one pad slot. `With(None)` clears
both slots; `With(key)` changes only the key slot; all other binding kinds
change the pad slot. This is convenient and matches the page. `IsHeld` ORs the
slots, so both a key and pad binding can activate the action simultaneously.

## Dead zones

The model layer contains no own dead-zone implementation. `InputBinding.IsHeld`
delegates stick checks to `GamePadSticks.Read`, so all held stick bindings use
the same circular threshold and exact 8-way quantization. The test suite proves
left/right stick isolation and screen-space Y storage. It does not directly
assert the dead-zone threshold in this model layer.

## Testability and coverage

The record structs are constructor-friendly and pure enough for unit tests.
Existing model tests cover display-name/parse round trips, key and stick held
state, separate device slots, None, garbage rejection, legacy stick spelling,
and screen-space Y.

Coverage gaps:

- Constructor validation for `PlayerInputState` components and
  `InputSnapshot.GetPad` index.
- Invalid numeric enum kinds/codes and gamepad-index 0/1 invariants.
- `InputBinding.CreateStick` with out-of-range values; clamping is documented
  only by behavior, not by its public constructor contract.
- `InputActionExtensions` unknown enum values.
- `InputSnapshot.Read` live hardware seam, while the test suite already
  constructs snapshots directly.

Recommended tests should remain deterministic and use `KeyboardState`,
`GamePadState`, and `Vector2` test values. They should assert contract
invariants at the model boundary, not only UI string round trips.
