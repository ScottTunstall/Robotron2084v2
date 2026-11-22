# Review: Input/Extensions (non-recursive)

## Scope

Reviewed `src/Robotron2084/Input/Extensions/InputActionExtensions.cs`.
No nested source directories exist below this folder.

## Mapping

`InputActionExtensions.GetLabel` is the display mapping for define-inputs UI
labels. Its switch covers every named enum member, with SHOOT RIGHT as the
fallback. `IsMove` classifies only the four move actions by exact enum membership;
all shoot actions are false.

The implementation is pure and cheap to test. There is no hardware or polling
dependency.

## Edge cases and testability

The fallback in `GetLabel` currently treats every cast or future enum value as
`SHOOT RIGHT`. C# permits `(InputAction)123`, and future enum additions would
silently receive the shoot-right label unless the switch is updated. The
existing enum has only eight members, so there is no known runtime bad value
from normal source. The safer contract would explicitly handle
`InputAction.ShootRight` and reject or report unknown values rather than treating
them as valid.

`IsMove` is total for known enum values and has no counterpart edge behavior.
The action enum is indexed directly in `PlayerControls._bindings`; invalid enum
values can still escape array bounds in callers. That class should validate
indices or enum values at public boundaries.

## Test guidance

Add or maintain a theory covering all eight labels and `IsMove` classification.
Add explicit unknown-value tests if enum validation is implemented. Keep these
tests pure and independent of MonoGame state.
