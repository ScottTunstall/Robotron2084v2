# Persistence Review

## Scope

Reviewed `src/Robotron2084/Persistence` and `src/Robotron2084/Persistence/Models` for serialization, file locations, corruption handling, format/version migration, and secret exposure. No source code was changed.

## Summary

The stores deliberately use plain, user-editable formats and generally have good missing-file behavior. Their main reliability issues are:

1. Saves use direct `File.WriteAllText` calls, so a crash or power loss can leave a truncated/corrupt file.
2. The high-score JSON format has no version and is not validated after deserialization.
3. Load/save exception handling is incomplete and differs across the stores.
4. Persisted files live in an obvious per-user folder in plaintext; they contain no application secrets, but names and settings are exposed to other code with access to the account.

## File paths and storage

- `AppDataPaths.GetFilePath` uses `%LocalAppData%\Robotron2084\<fixed-name>` (`AppDataPaths.cs:4-13`). The fixed callers use `highscores.json`, `controls.ini`, and `gameSettings.ini`.
- The public `GetFilePath(string fileName)` helper does not reject absolute paths or path separators. It is not currently used with user input, but a caller could pass a path that escapes the application folder. Callers should treat it as an internal fixed-name helper.
- A missing or unavailable local-app-data location can produce an unexpected relative path or an empty base folder. Load currently relies on `File.Exists` plus fixed test seams; no path normalization or directory validation is performed.
- Directory creation occurs in `Save` immediately before writing. Save does not catch and wrap I/O failures, unlike Load, so callers receive raw `IOException`/`UnauthorizedAccessException` rather than `PersistenceException`.
- Concurrent application instances are not coordinated. Direct writes can race and lose updates.

## Serialization findings

### High-score JSON

- `HighScoreStore.Load` reads the entire file and deserializes `SavedTable` (`HighScoreStore.cs:24-32`). A valid JSON object with the wrong shape is mostly handled leniently: missing top/allTime values fall back by `?.`, while a missing or wrong-typed nested value can take fallback or throw `JsonException`.
- The format is explicitly pretty-printed on save (`HighScoreStore.cs:54-62`) and has no BOM/encoding configuration. Nested records serialize by their public property names: `Initials`, `Score`, `Name`; `SavedTable` is mapped to `"top"` and `"allTime"`.
- Unknown JSON properties are accepted by default. This is permissive, but no format version or schema marker exists (`Models/SavedTable.cs:1-11`).
- `JsonSerializer.Deserialize` is not bounded by file size or nesting depth, and `File.ReadAllText` allocates the whole file. This is primarily a local corruption or disk-damage concern, not a network threat, but it is still a resilience weakness for a hand-editable file.
- JSON is saved directly. `File.WriteAllText` truncates the existing file before it finishes writing, so a crash or process termination can leave invalid JSON.

### INI-like files

- Game settings and controls are plain UTF-8 without a BOM (`GameSettingsStore.cs:91-92`, `ControlSettingsStore.cs:93-94`). Reading uses `File.ReadAllLines`, so decoding replacement/error behavior is handled by .NET's text reader rather than the stores.
- Both parsers start from factory values and apply only recognized, valid fields. Unknown sections and keys are ignored, duplicate names use the last parsed valid value, and malformed individual values do not discard the whole file.
- These files have no explicit format version. Section/key renaming is handled only by hand-maintained compatibility logic, such as the control store accepting legacy pause `key`/`pad` names (`ControlSettingsStore.cs:111-118`).
- INI values are also written directly with no temp file, backup, lock, or atomic replacement.

## Corruption and recovery

- Missing files are deliberately non-errors and return factory settings/scores (`HighScoreStore.cs:24-27`, `GameSettingsStore.cs:35-41`, `ControlSettingsStore.cs:34-42`).
- A race where the file disappears after `File.Exists` is handled for each Load method.
- Existing unreadable or malformed files produce `PersistenceException` for JSON parse and read/open failures and for INI read/open failures. The message includes the path.
- INI `Parse` methods are resilient to unknown or invalid lines. A file containing no recognized settings is effectively a factory reset.
- `SecurityException`, `NotSupportedException`, `ArgumentException`, and `PathTooLongException` are not consistently converted to `PersistenceException`. Save has no wrapping at all.
- The stores do not create a backup or retain a previous good copy. A malformed JSON file remains malformed until a successful Save, and a crash during any direct write can produce that malformed state.
- `HighScoreStore.Load` uses no post-deserialization validation. A corrupt or hand-edited JSON can contain:
  - negative or implausible scores;
  - scores outside the ROM's seven-digit range;
  - initials longer than three characters or null strings;
  - a top name longer than 23 characters or null;
  - more than 36 all-time entries (these are truncated later);
  - an unsorted all-time list;
  - null list elements;
  - duplicate entries or inconsistent top/all-time rankings.

  `CreateFromSaved` only pads/truncates and substitutes fallback entries; it does not validate or sort (`HighScoreTable.cs:165-170`, `HighScoreTable.cs:263-272`). Null list elements or null strings can later cause null-reference failures in methods that read `.Score` or `.Initials`.

- Game settings validate numeric ranges and enums on load (`GameSettingsStore.cs:104-137`). Unknown or out-of-range values keep the factory value.
- Controls validate binding vocabulary and player/action names; unknown values leave each slot at its factory value (`ControlSettingsStore.cs:76-128`).

## Migration and versioning

- The JSON format has an implicit only: property names and CLR record shape. It has no `"version"`, migration routine, or schema hash.
- Renaming a nested property may deserialize a missing/null value and trigger fallback behavior that silently loses data. Unknown-property support alone does not establish backward/forward compatibility.
- The INI format is more extensible, because unknown keys/sections are ignored, but it likewise has no version. Semantics can change without any way to detect or migrate a file.
- Use of saved JSON property names without explicit `JsonPropertyName` on nested entries means future renaming/refactoring of CLR properties can alter the on-disk format. The nested records currently rely on default `System.Text.Json` property names.
- Recommended migration design: add a top-level format version, explicit named properties, a tolerant reader, range/shape validation, and a migration map. Preserve a backup and only replace the live file atomically after a complete write.

## Secrets

- No passwords, API keys, credentials, or token fields are present in this area.
- Persisted data is high-score names/initials, control bindings, and gameplay settings. It is plaintext and not encrypted or integrity-protected.
- User-entered high-score/top names may be personal data, although they are not credentials. Avoid logging file contents and use normal account-level ACLs inherited from `%LocalAppData%`.
- `%LocalAppData%` is generally user-specific, so these files should not contain secrets for the application. If secrets are introduced later, they must not be stored here as plaintext.
- No secret values appear in the comments or examples reviewed.

## Priority recommendations

1. **High — add JSON and model validation:** enforce top/all-time/top-name limits, three-character initials, 0–9,999,999 scores, descending all-time order, and reject or sanitize null entries.
2. **High — make Save atomic:** write a temp file in the same folder, flush/close it, then replace the live file with backup/rollback support.
3. **Medium — add a persisted format version:** version JSON explicitly and add equivalent metadata to INI formats if compatibility is expected.
4. **Medium — wrap save and uncommon load errors:** consistently surface `PersistenceException` naming the file and preserve missing-file behavior.
5. **Medium — remove traversal risk:** make `GetFilePath` restrict file names to a simple file name or remove its public exposure.
6. **Low — size-limit JSON input:** impose a maximum byte size and depth appropriate for 36 entries plus the top entry.
