# Code Review: Entity Collections

## Scope

Reviewed the C# files directly inside `src/Robotron2084/Entities/Collections` (non-recursive): `EntityList<T>`, `IEntityList`, and all concrete entity-list classes. I also inspected collection call sites in `src/Robotron2084` and the collection tests in `tests/Robotron2084.Tests/Entities/Collections`.

## Findings

1. **Medium — Base `Add` accepts null and defers failure to later enumeration**
   - **Location:** `src/Robotron2084/Entities/Collections/EntityList.cs:47-49`
   - `T` is constrained to `class, IEntity`, but `EntityList<T>.Add(T entity)` does not reject `null`. `FamilyList.Add` correctly guards, but the base API and every other derived list currently do not. A null can be inserted because C# permits `null!`; subsequent `IsAlive`, `Update`, or `Draw` calls will produce a `NullReferenceException`.
   - **Fix:** At the start of `EntityList<T>.Add`, call `ArgumentNullException.ThrowIfNull(entity);` before `_items.Add(entity)`. This makes null rejection consistent for every list and leaves `FamilyList`'s existing guard redundant but harmless.
   - **Test:** Add a test asserting `ElectrodeList.Add(null!)` throws `ArgumentNullException` and that no item is inserted.

2. **Medium — `FamilyList` can assign slots outside its declared 40-slot model**
   - **Location:** `src/Robotron2084/Entities/Collections/FamilyList.cs:43-54`
   - `_nextSlot` is monotonically incremented without checking `SlotCount`. After the 40th member, `FamilySlot` becomes 40 and higher; these values are outside `0..SlotCount - 1` (lines 36-41). `GetMemberInSlot`, nearest-slot search, and hulk targeting cannot reliably represent such members.
   - **Fix:** Before assigning a slot, if `_nextSlot >= SlotCount`, throw `InvalidOperationException` (or another documented capacity exception). Prefer a capacity guard based on `_nextSlot`/`Count`, and do not increment until after a successful `base.Add` if central Add behavior may fail.
   - **Test:** Extend `FieldStrategyTests` or add a `FamilyListTests` case that adds `SlotCount + 1` members and verifies the capacity exception and unchanged count.

3. **Low — `SpheroidList.CountForNextLife` does not validate negative public inputs**
   - **Location:** `src/Robotron2084/Entities/Collections/SpheroidList.cs:17-25`
   - `enforcersLeft` and `startedWith` are public caller inputs but are neither validated nor bounded. Negative enforcer counts can produce negative `spheroidsFromEnforcers`; negative `startedWith` is returned unchanged as the minimum and also violates the method's meaning of a starting count.
   - **Fix:** Reject negative values with `ArgumentOutOfRangeException`, e.g. `if (enforcersLeft < 0) throw ...;` and `if (startedWith < 0) throw ...;`. Alternatively document and normalize, but public API validation is more reliable.
   - **Test:** Add theory cases for `-1` enforcers and `-1`/negative `startedWith`, asserting the chosen exception behavior.

## Additional Inspection

- The existing collection tests cover alive/dying/dead counts, electrode clearing, grunt speed, spheroid conversion, and several family-slot behaviors. They do not cover null insertion, family-list capacity, or invalid spheroid conversion arguments.
- `UpdateAll` and `DrawAll` iterate the backing `List<T>` directly. In the inspected normal call flow, entities updated in one kind's list are not observed adding/removing entries from that same list before the end of the update pass; pruning happens only after all update and collision passes. No concrete same-list mutation-while-enumeration bug was confirmed.
- `PruneDead` uses `List<T>.RemoveAll`, which is not an external foreach and is safe from this pattern.
- `IReadOnlyList<T>.FieldEntities` exposes an `IEnumerable<IEntity>` but does not expose a mutable backing list, and typed `this[int]` delegates normal bounds checking to `List<T>`.

## Summary

Three collection API issues should be addressed: add a base null guard, enforce the 40-place family-list capacity, and validate spheroid conversion inputs. No source changes were made.
