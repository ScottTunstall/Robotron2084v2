# Review: Events/Models

## Scope

Reviewed the C# files directly contained in `src/Robotron2084/Events/Models`:

- `GameEvent.cs`
- `GameEventKind.cs`

This review covers model invariants, null safety, duplicate/delivery semantics, ordering,
and thread safety. Entity or hub behavior that supplies `GameEvent` values is referenced
where relevant.

## Findings

### High: `GameEventKind` has no invalid-value protection

- `GameEventKind` is a public enum with numeric values for twelve named event kinds.
- C# permits `(GameEventKind)123` and every other out-of-range integer cast.
- `GameEvent` does not validate the enum in any constructor, so an arbitrary code value can
  be passed without exception.
- Existing switches or listeners would need to handle unknown values explicitly.
  The model provides no `Enum.IsDefined` behavior or named/error fallback.

At public APIs and deserialization boundaries, reject unknown values with
`ArgumentOutOfRangeException` or define a complete unknown-event fallback. Internal raisers
should also be checked if future enum members are added.

### Medium: `GameEvent` does not enforce kind-specific invariants

`GameEvent` is a positional `readonly record struct`:

```csharp
new GameEvent(GameEventKind Kind, IntVector2 Position, RobotKind? Robot = null)
```

It has no constructor validation. The XML documentation defines relationships, but the
record does not enforce them:

- `Position` should be `IntVector2.Zero` for place-independent kinds such as
  `WaveStarted` and `WaveCompleted`.
- `Robot` should be non-null for `RobotKilled` and null for every other kind.
- Unknown `GameEventKind` values do not have a documented position/robot contract.

The result is an accepted value that may contain valid types but invalid meaning, for example
`RobotKind.Grunt` on `PlayerKilled` or null `Robot` on `RobotKilled`. Because there is no
constructor body, typos and future callers are not caught at creation.

Any validation should be intentional and preserve the intended use of `IntVector2`.
At minimum, document and test `RobotKilled`/`Robot` and place-independent kinds. If strict
invariants are required, move to an explicit record constructor or factory and validate
`Enum.IsDefined`.

### Medium: Nullable `Robot` is safe but not semantically constrained

- `RobotKind?` is the only reference-like nullable field in the model.
- The record struct has no null dereferences; a listener should still not assume
  `gameEvent.Robot.HasValue` based only on the enum if invalid construction is allowed.
- All non-`RobotKilled` kinds are documented to have `Robot == null`, but the model does not
  enforce that contract.
- `RobotKind` itself is an enum and may contain unknown numeric values even when `Robot` is
  non-null.

### Low: `IntVector2.Position` has no cross-kind placement validation

- `Position` is an immutable value carried with the event.
- The record does not enforce that a place-based event has an in-bounds position or that
  place-independent events use `IntVector2.Zero`.
- Entity implementations appear to pass their current `Position` consistently, and
  `WaveStarted/WaveCompleted` pass `IntVector2.Zero`.
- The event model cannot know the playfield bounds, so positional bounds validation belongs
  at the producer or playfield boundary rather than in this record.

### Low: Record struct immutability helps thread safety

- `GameEvent` is a readonly record struct.
- A valid instance can be copied by value and has no mutable collection or object state.
- `IntVector2` is used as a value field, and `RobotKind?` is a value/nullable value.
- Once created, an event is immutable and can safely be passed to listeners on any thread
  without an internal mutation race.
- Thread safety here is therefore about the hub and listener handling, not event contents.

This does not make cross-thread delivery ordered or make a hub snapshot safe. `GameEvent`
itself is a plain immutable message.

### Low: Enum and model do not carry event sequence or duplicate identifiers

- The event has kind, position, and optional robot only.
- It has no timestamp, sequence number, unique ID, source entity ID, or generation/wave ID.
- Duplicate delivery cannot be distinguished from two legitimate identical events:
  for example, two identical `PlayerLaserDestroyed` events at the same position can produce
  the same `GameEvent` value.
- Record equality would therefore consider semantically distinct deliveries equal, but there
  is no deduplication contract in the record.

If the system may need exactly-once semantics, add an explicit event ID or sequence outside
the readonly record. If exactly-once is not required, document that every raise is a delivery
and that producers own duplicate suppression.

## Ordering, Duplicate, and Listener Implications

- `GameEvent` order is not stored inside the model. There is no sequence field or ordering
  key.
- Raise call order is imposed by `GameEventHub.Raise`, not by `GameEvent` fields.
- Two consecutive producer calls may create distinct struct values only if their kind,
  position, or robot differ. Equal values can be intentionally or accidentally raised again.
- Repeated subscriptions cause each event value to be delivered repeatedly; repeated
  producer raises can likewise deliver repeated events. The model cannot prevent either.
- Strong producer guards (for example `LifeState = Dead` or `IsAlive()` checks) reduce some
  repeated raises, but these are not part of the event model and are not checked atomically
  by the hub.

## Test Coverage

There are no direct tests for `GameEvent` or `GameEventKind` invariants in the event test
folder. `GameEventHubTests` constructs valid values and does not test:

- unknown `GameEventKind` values
- invalid numeric `RobotKind` values in `Robot`
- `RobotKilled` with null `Robot`
- non-`RobotKilled` kinds with non-null `Robot`
- place-independent kinds with non-zero `Position`
- place-dependent kinds with unknown/out-of-bounds positions
- record equality or duplicate-identification semantics

## Summary

The model is small, immutable, and naturally copy-safe. Its main weakness is that it is a
data carrier rather than an invariant boundary: unknown enum values and kind-specific
`Position`/`Robot` combinations are accepted. It has no sequence or duplicate identifier,
so deduplication must remain the hub or producer's responsibility. Thread safety of event
instances is good, but delivery ordering and duplicate behavior belong to `GameEventHub`
and producer lifecycle guards.
