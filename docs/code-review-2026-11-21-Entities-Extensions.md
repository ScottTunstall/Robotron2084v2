# Code Review: Entity Extensions

## Scope

Non-recursive review of the C# files directly under
`C:\Source\Robotron2084v2\src\Robotron2084\Entities\Extensions`:

- `EntityExtensions.cs`
- `EntityLifeExtensions.cs`
- `FamilyMemberKindExtensions.cs`

I also inspected direct call sites and the relevant entity, enum, texture, collision, and test code. No source files were changed.

## Findings

### 1. `GetAs<T>` throws `NullReferenceException` for a null entity

**Severity:** Medium

**Reference:** `EntityExtensions.cs:9-13`

`entity as T` produces `null` both for a null input and for an entity of the wrong runtime type. The expression in the null-coalescing fallback then evaluates `entity.GetType()`, so a null receiver throws `NullReferenceException` rather than the method's documented `InvalidOperationException`.

```csharp
return entity as T ??
       throw new InvalidOperationException($"{entity.GetType().Name} ...");
```

**Fix:** First test for null explicitly and throw `ArgumentNullException(nameof(entity))`. Then test the cast and throw `InvalidOperationException` for a non-null wrong type. Update the XML exceptions accordingly.

**Test gap:** There are no direct tests for `GetAs<T>`.

### 2. Unknown `FamilyMemberKind` values silently become Daddy

**Severity:** Medium

**References:**

- `FamilyMemberKindExtensions.cs:15-21`
- `FamilyMemberKindExtensions.cs:27-33`

Both switch expressions use `_ => ...Daddy...`. An enum value cast outside `Mikey`, `Mommy`, and `Daddy` will therefore be treated as Daddy in both animation and collision-size lookups. That is unsafe for persisted or deserialized input and hides data corruption instead of surfacing it.

**Fix:** Replace each catch-all with `FamilyMemberKind.Daddy` and add a `default => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown family member kind.")` case. If the enum is intentionally closed, this is best enforced at the mapping boundary.

**Test gap:** No tests exercise invalid enum values or assert the three family-kind mappings directly.

### 3. Family texture lookup returns and exposes the shared mutable texture array

**Severity:** Low/Medium

**Reference:** `FamilyMemberKindExtensions.cs:15-21`

The method returns `TextureSet.MikeyAnimationFrameTextures`, `MommyAnimationFrameTextures`, or `DaddyAnimationFrameTextures` directly. Those properties are public mutable `Texture2D[]` properties, and this method exposes the same backing array. A caller can replace or reorder entries and thereby alter the animation for every later family member and converted prog using that texture set.

**Fix:** Prefer a read-only view/API (for example `IReadOnlyList<Texture2D>` backed by a protected/copy-owned array) or, where a copy is acceptable, return `.ToArray()`. At minimum, prevent callers from obtaining a modifiable reference to `TextureSet`'s backing storage.

**Test gap:** No family-kind extension or texture-aliasing test is present.

### 4. Entity life extensions do not defend against null receivers

**Severity:** Low

**References:**

- `EntityLifeExtensions.cs:19-22`
- `EntityLifeExtensions.cs:26-29`
- `EntityLifeExtensions.cs:32-35`

Calling any of `IsAlive`, `IsDead`, or `IsDying` with a null `IEntity` causes a null-dereference. The project currently has nullable reference checking disabled in `Robotron2084.csproj`, so the compiler does not signal this boundary in callers.

**Fix:** Add explicit `ArgumentNullException` checks in all three methods, or enable nullable reference types and annotate/validate entity inputs consistently.

**Test gap:** `EntityOwnRulesTests.cs:123-136` covers normal life-state transitions, but not null entity receivers or invalid enum values.

## Summary

The life-state mapping itself is straightforward and is reasonably covered for Alive, Dying, and Dead transitions. The clearest correctness concern is the family-kind mapping's catch-all fallback, which silently converts every unknown enum value to Daddy. There are also boundary issues in `GetAs<T>` and the life extensions for null receivers, plus a mutable-array alias in the family texture lookup.

No mutation or hidden behavior was found in the life-state methods themselves. The most important fixes are explicit enum validation and explicit null validation; direct tests should cover wrong/null casts, invalid family kinds, and texture-array aliasing.
