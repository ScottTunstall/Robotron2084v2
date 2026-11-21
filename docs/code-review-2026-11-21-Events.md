# Code Review: C# Events

## Scope

Reviewed the C# files directly contained in `src/Robotron2084/Events`:

- `GameEventHub.cs`
- `IGameEvents.cs`
- `IGameEventListener.cs`
- `NullGameEvents.cs`

The review also checked direct subscribers/producers and existing event tests. Files in
`src/Robotron2084/Events/Models` are covered separately in `Models/REVIEW.md`.

## Findings

### High: `GameEventHub` is not thread-safe

- `GameEventHub` stores subscribers in `List<IGameEventListener>` and provides no lock,
  snapshot, or concurrent collection.
- `Raise` and `ListenerCount` read the list while `Subscribe` writes it.
  Concurrent subscription and delivery can throw `InvalidOperationException` from
  `foreach`, and the result is race-dependent.
- Concurrent `Raise` calls are also unsynchronized. They can interleave listener calls,
  so separate threads do not have a single externally observable event order.
- `ListenerCount` can observe a write or count update without synchronization.

This class is currently a hub for frame/game-state code, but it has no threading contract.
It should either document and enforce calling from one thread or make the subscription
and delivery operations synchronized/concurrent. A lock with a snapshot is preferable
where listener callbacks must not run under the hub lock.

### Medium: Delivery is reentrant and does not preserve “raised first” ordering

- `Raise` synchronously calls listeners in subscription order.
- It does not queue events. If listener A handles event Y and calls `Raise(X)`, nested
  delivery of X can complete for all listeners before listener B handles Y.
- Listener mutations can also invalidate or alter the live `List<T>.Enumerator`.
  A subscriber that subscribes during `Raise` may affect the in-progress enumeration;
  a concurrent or reentrant modification is unsafe.

For predictable semantics, the hub should use a snapshot and define reentrant behavior:
either preserve call-stack nesting with a snapshot per raise or queue events and deliver
one complete event to all listeners before the next.

### Medium: Listeners have no unsubscribe or lifecycle boundary

- `Subscribe` is the only listener-management operation. There is no `Unsubscribe`,
  disposal, weak subscription, or clear operation.
- Every subscribed object is kept strongly reachable until the hub is discarded.
- The listener remains active for the entire lifetime of the hub even if the listener's
  owner is gone or its scene/state no longer exists.
- A listener cannot safely self-unsubscribe, because no unsubscribe API exists.

The hub should expose `Unsubscribe(IGameEventListener)` and perhaps document snapshot
semantics for removing a listener during `Raise`. If listeners are stateful and short-lived,
weak references may also be useful, but strong unsubscribe is the minimal requirement.

### Medium: Duplicate subscription causes duplicate delivery

- `Subscribe` always calls `_listeners.Add(listener)`; it does not test for an existing
  reference.
- Subscribing the same instance twice adds two entries and raises the listener twice for
  each event.
- Subscriber count and lifecycle are therefore not unique-listener counts despite
  `ListenerCount` being described as “How many listeners are subscribed.”
- There is no duplicate prevention and no unsubscribe to repair a mistaken or repeated
  subscription.

The intended contract should be explicit: either subscriptions are reference-unique and
`Subscribe` is idempotent, or duplicate subscriptions are intentionally allowed and the
API uses a subscription handle/token.

### Medium: One listener exception stops and fails the whole delivery

- `Raise` has no `try/catch` around `listener.Handle(gameEvent)`.
- A listener exception immediately aborts the loop, so later listeners receive no event.
- The exception may prevent a producer's state transition or leave an entity's cleanup
  partially complete.
- No listener error-policy is defined: whether callbacks are best-effort, fatal, or isolated.

The hub should state one policy and implement it. If one listener must not block others,
capture/forward exceptions after delivering to every listener. If listener exceptions are
fatal, snapshot and document that raising is not a completion barrier.

### Low: Listener ordering is synchronous and implicit

- The only delivery guarantee is insertion order: `Raise` visits the live list from first
  to last.
- There is no priority, phase, or ordering API.
- The order is therefore the incidental order of `Subscribe` calls.
- A snapshot of the subscriber list would make the beginning of a raise predictable,
  even though it would not solve reentrant raises or cross-thread raises.

This is suitable for a simple in-state hub only while all subscribers are installed once
and no callback mutates the subscription set.

### Low: `Raise` has no raise-time null check

- `GameEvent` is a readonly record struct, so C# callers cannot pass `null` to
  `IGameEvents.Raise`.
- `IGameEventListener.Handle` similarly receives a value.
- `NullGameEvents.Raise` is valid and intentionally ignores the event.

There is no null-safety defect here. The nullable concern belongs to fields in
`GameEvent`, not to `Raise` arguments.

## Duplicate/Repeated-Event Risks in Producers

The producer classes do not generally raise from the hub multiple times by accident:
many kill/rescue methods guard with `IsAlive()` or set `LifeState` to `Dead` before
raising. Examples include `Gorf.Kill()`, `Grunt.Kill()`, and `FamilyMember.Kill/Rescue`.
However, those are producer-local guards, not guarantees enforced by `GameEventHub`.
The hub will deliver every call it receives, and duplicate state-transition calls can
therefore become duplicate events if a producer forgets or changes a guard.

Some event-sensitive methods also raise without a hub-level deduplication key or sequence
identifier. `WaveCompleted` and `WaveStarted` have producer-local “once” behavior
(`RaiseWaveStartedOnce()` and the wave-clear state), but re-entering or invoking those
paths again is not prevented by the event infrastructure.

## Existing Test Coverage

`tests/Robotron2084.Tests/Events/GameEventHubTests.cs` covers:

- `null` subscription refusal
- raising with no subscribers
- sequential event order for one listener
- delivery to all subscribers
- `NullGameEvents` no-op behavior

Missing focused cases include:

- repeated `Subscribe` of the same listener
- `Unsubscribe` (API absent)
- listeners that subscribe or raise during delivery
- listener exception behavior
- concurrent raise/subscription/listener count
- preserving call order across listeners for nested raises
- duplicate event delivery from repeated producer calls

## Summary

The event transport is small and simple, but its behavior is only safe for the single,
synchronous, fixed-listener case. The main correctness risks are missing thread safety,
no unsubscribe/lifecycle API, duplicate delivery from repeated subscriptions, live-list
mutation during delivery, reentrant ordering, and listener exceptions stopping the
remaining callbacks. The value-type event argument and singleton null implementation
are otherwise null-safe.
