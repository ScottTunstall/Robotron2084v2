# Review: `tools/SpriteExtractor`

## Scope and validation

Reviewed `tools/SpriteExtractor/Program.cs` and
`tools/SpriteExtractor/SpriteExtractor.csproj`. No source was changed.

Validation:

- `dotnet build tools\SpriteExtractor\SpriteExtractor.csproj --no-restore`
  fails:
  - `Program.cs:467` calls `FromColourValue`, but the method is named
    `CreateFromColourValue`.
  - `Main`, `ExtractOne`, and `CollectData` exceed the configured CA1502
    complexity threshold and are build errors.
- The tool cannot currently execute or generate PNGs.

## High-priority findings

### The palette method name prevents compilation

- `GamePalette.Colours` calls `FromColourValue`, but no such method exists.
  The implementation is `CreateFromColourValue`.
- This is an immediate blocker. It should be renamed consistently or the
  initializer should call the existing method.

### Pass B may emit `FDB` words in the wrong byte order

- `DataValues` detects `FDB` and currently appends
  `(byte)(v & 0xFF)` followed by `(byte)((v >> 8) & 0xFF)`.
- A Motorola 6809 `FDB` is a 16-bit word stored high byte first, so it should
  append the high byte before the low byte.
- This can corrupt source-extracted frames and make them fail or appear in
  the wrong byte orientation. It also weakens the claim that source bytes
  are ROM-verified.
- Correct and add ROM-backed tests for each `FDB` label/wanted frame, including
  values with distinct high and low bytes.

### Full-run and validation behavior can report success after partial failure

- Malformed list rows, out-of-range rows, inline length mismatches, and
  `NOT FOUND` source sequences are collected as warnings and printed.
- `Main` returns 0 regardless of warning count.
- The tool can leave old PNG files from previous runs and write a map that
  omits the skipped entries. A partially successful run therefore looks
  successful and stale assets remain.
- Return a nonzero status when any required frame is skipped or when any warning
  is fatal; add a separate diagnostic mode for warnings that are explicitly
  informational. Remove or quarantine stale generated files before publishing
  a newly generated set.

## Correctness and bounds findings

### Integer overflow can bypass Pass A range checks

- Pass A evaluates `w * h` and `off + w * h` using `int`.
- Malformed list values or an overflow can make the multiplication negative,
  bypass `w * h > rom.Length`, and potentially create an invalid slice.
- Use checked/long arithmetic: require `w >= 1`, `h >= 1`,
  `w <= rom.Length`, `h <= rom.Length/w`, and
  `off >= 0 && off <= rom.Length - expected`.
- Apply the same bounds discipline to extras and all slice operations.

### CLI input is read before argument validation

- `Main` reads `robotron64k.bin` before parsing `--sprite`, `--quiet`, or
  unknown options.
- A typo or missing ROM is therefore reported as an unhandled file exception
  rather than concise usage or a bad-argument exit.
- Parse and validate arguments first, validate required paths and ROM size,
  and catch I/O exceptions at the CLI boundary.

### ROM search does not establish a unique sprite match

- `FindBytes` returns the first literal match for Pass B data.
- An incidental repeated byte sequence can make a frame appear at the wrong
  offset even though the bytes match. It does not check a frame boundary or
  a source label/reference.
- Return ambiguous matches as warnings and require a unique or explicitly
  known match for automatic extraction.

## CLI and platform findings

### Repository discovery depends on the caller's working directory

- `FindRepoRoot` starts at `Environment.CurrentDirectory` and walks parents
  until `ref` and `src` both exist.
- Running the tool from a sibling directory or through a different launcher
  can make it fail even though the executable and project are inside the repo.
- Derive the checkout from `AppContext.BaseDirectory`, an explicit `--root`
  option, or an environment/documented invocation. Do not assume that invoking
  `dotnet run` leaves the process in the repository root.

### Output names are Windows filename assumptions and are not collision-safe

- Only `largefont:` is explicitly remapped to `Font_L_colon`. Other names
  should be checked for illegal Windows characters, trailing dots/spaces,
  reserved device names, and case-insensitive collisions.
- Full extraction can overwrite a file if two source names map to the same
  output name. Pass B appends a label suffix to duplicates only for
  `emitted.Contains(png)`; collisions in Pass A/C are not handled.
- Validate mapped names once, fail or choose deterministic unique names, and
  check both Windows and case-insensitive destinations.

### Source-layout assumptions are brittle

- `Main` assumes `ref/rom/robotron64k.bin`,
  `docs/robotronsprites.txt`,
  `src/Robotron2084/Content/Sprites`,
  `docs/sprite-map.md`, and `ref/original-source`.
- Missing directories or files cause unhandled `IOException`/
  `DirectoryNotFoundException` in a full or Pass B run.
- Report each missing input with the exact expected path and a nonzero exit.

## Resource lifetime and I/O findings

- All file streams are scoped with `using`; `ZLibStream` uses `leaveOpen: true`
  and is also scoped, so managed stream lifetime is otherwise handled
  correctly.
- `File.ReadAllBytes` loads the 64K ROM once. `File.ReadAllLines` loads each
  source/list file and is bounded for this repository, but it retains no
  long-lived resources.
- `ZlibCompress` and the raw PNG buffers are correctly bounded by dimensions;
  however, malformed dimensions can create very large allocations before
  bounds are checked. Validate dimensions against ROM and image limits before
  allocating.
- `WritePng` creates/truncates the destination with `File.Create` before all
  PNG chunks are written. A write failure can leave a partial PNG. For
  production use, write beside the destination in a sibling temporary file
  and atomically replace it only after successful completion.

## PNG correctness

- The PNG signature, IHDR, IDAT, IEND, filter byte, compression, CRC-32, and
  RGBA row layout are implemented directly and are structurally complete.
- Pixel extraction uses high nibble as left and low nibble as right, matching
  the stated sprite format.
- Palette index 0 is transparent unless `solid` is true.
- Inline data is checked for exact `w*h` length, and that is good. There is no
  independent PNG verification or expected-file golden test.
- Add tests that decode generated PNGs and assert dimensions, known row bytes,
  solid/transparency behavior, CRC correctness, and exact RGBA values.

## Positive observations

- The tool avoids external packages and directly owns its PNG writing.
- Pixel, row, and PNG filter data are bounded to explicit width/height loops.
- `ExtractOne` creates the output directory only for a requested extraction,
  and all streams are disposed.
- Pass A range checks and inline extra-length checks reject many obvious
  malformed values.
- The ASCII preview and palette legend provide useful manual validation.

## Recommended tests

1. Palette conversion, including all 16 indices and an image with only index 0
   in solid and transparent modes.
2. Build after renaming or aliasing `FromColourValue`.
3. Pass A malformed rows, negative/overflowing widths/heights/offsets, and
   missing ROM/list files.
4. Pass B FCB/FDB decoding, with and without symbolic pointer rows, and
   ambiguous/first-match ROM searches.
5. CLI missing/unknown options, output name collisions, stale-output cleanup,
   and nonzero exit on skipped required frames.
