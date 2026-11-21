# Persistence Models Review

## Scope

Reviewed the persistence model types in `src/Robotron2084/Persistence/Models` for serialization shape, invariant enforcement, corruption resilience, versioning, and secret exposure.

## Files

| File | Role |
|---|---|
| `SavedTable.cs` | JSON envelope: top entry and all-time list |
| `HighScoreEntry.cs` | Three-initial/name and score pair |
| `TopScoreEntry.cs` | Top name and score pair |
| `SubmitResult.cs` | In-memory result of submitting a score |

## Serialization findings

### `SavedTable`

- Uses explicit JSON names `"top"` and `"allTime"` (`SavedTable.cs:8-11`), which is a useful stability aid.
- Both properties are nullable. That allows missing fields to be detected, but also permits JSON that is valid yet semantically incomplete.
- The all-time property is `List<HighScoreEntry>?`, while the loader's fallback logic makes a null or empty list select ROM defaults.
- The JSON wrapper is internal and has no format-version field. Unknown or renamed future properties cannot be distinguished from corruption or an old format.

### `HighScoreEntry`

- It is a public positional record, serialized by default as `"Initials"` and `"Score"` (`HighScoreEntry.cs:1-21`).
- Constructor parameters have no validation:
  - `Initials` can be null or longer/shorter than three characters;
  - `Score` can be negative or exceed the ROM's seven-digit range;
  - JSON deserialization can create a null `HighScoreEntry` element in a list.
- `GetDisplayName` trims whitespace. It is a display helper, not validation (`HighScoreEntry.cs:16-19`).
- The `"   "` blank entry is representable from JSON too. JSON should probably forbid blank entries except for the exact known row fill behavior.
- The class's documented ROM invariant is not enforced in the type or deserializer.

### `TopScoreEntry`

- It is a public positional record serialized as `"Name"` and `"Score"` (`TopScoreEntry.cs:1-15`).
- It has no validation on name length or NUL/whitespace content. A name can be null or longer than `TopNameLength` (23).
- Negative or out-of-range scores are not rejected.
- The property names depend on default JSON naming. Refactoring `Name` or `Score` renames the on-disk schema even though `SavedTable` itself has explicit names.

### `SubmitResult`

- This is an in-memory `record struct`, not a persisted model (`SubmitResult.cs:1-14`). It has no serialization or storage concern.

## Corruption and invariant findings

1. **No post-deserialization invariant validation.** `HighScoreStore.Load` passes deserialized values into `HighScoreTable.CreateFromSaved`. That method falls back only for a null top or null/empty all-time list and pads/truncates. It does not validate individual entries.
2. **Unsuitable entries can be accepted.** `CreateFromSaved` accepts an arbitrary list, including negative scores, malformed initials, and entries outside the ROM's score range (`HighScoreTable.cs:165-170`, `HighScoreTable.cs:263-272`). The result can produce incorrect leaderboard ordering and qualification behavior.
3. **Sorting is not enforced.** A hand-edited or corrupted all-time list may be unsorted. `Insert` relies on score order and `Beats` reads the last entry, while a saved list is not sorted before use.
4. **Null elements are a crash risk.** A JSON list containing `null` is plausible at runtime even though `List<HighScoreEntry>` is intended non-null. `_allTime[^1].Score`, `FindIndex`, and `CountInitials` may dereference null elements.
5. **Top/all-time consistency is not checked.** A loaded top score may not be the highest entry, and a loaded all-time list may contain equal or higher entries.
6. **Capacity is not the sole invariant.** The table pads to 36 all-time and 10 today entries. A JSON all-time list with zero entries falls back to factory rows; a partially populated list is padded with blank rows, but no source attribution or validation marker remains.
7. **No explicit schema constraints.** `HighScoreTable.InitialsLength`, `AllTimeCapacity`, `TopNameLength`, and score limits exist in the table class, but the persisted record types do not reference or enforce them.
8. **The all-time fallback logic may conceal corruption.** An empty list is interpreted as “use factory all-time,” while a list of invalid but non-empty entries is accepted. The loader cannot distinguish an old or corrupt file from a deliberately incomplete one.

## Migration and versioning findings

- `SavedTable` is a schema-only envelope and does not carry a version.
- Adding/removing JSON fields is therefore implicit. Missing nested fields silently become null/zero/default behavior, and old property names cannot be migrated deliberately.
- Default nested property serialization makes future CLR property renames silently alter the disk format.
- Add explicit `[JsonPropertyName]` attributes to all persisted models, or use an immutable serialized DTO separate from domain records, and add a required format version at the envelope level.
- Migration should be transactional: parse and validate an old version, migrate in memory, and write an atomically replaced current-version file only after successful validation.

## Secret exposure

- There are no secret-bearing fields in these model types.
- `TopScoreEntry.Name` and `HighScoreEntry.Initials` are player/operator text and may contain personal data, but there is no evidence of credentials or authentication material.
- JSON stores these values in plaintext. The data should remain local and should not be logged or copied into telemetry.
- All model properties are public/value-visible and have no protection or integrity checks. A local user can edit them directly.

## Priority recommendations

1. **High — enforce invariants in deserialization or loader validation:** non-null names/initials, exact initial length, name length ≤23, 0–9,999,999 scores, descending all-time order, and no null elements.
2. **High — validate top/all-time consistency:** ensure the top is not below an all-time entry and sanitize or reject contradictory data.
3. **Medium — version and explicitly name every serialized property:** keep JSON stable across future record-property renames.
4. **Medium — split persisted DTOs from domain types:** prevent deserialization from constructing invalid domain state and make migration code explicit.
5. **Low — treat list shape as a validation concern:** distinguish missing/empty lists, partially malformed lists, and fully valid lists rather than using empty as an implicit factory fallback.
