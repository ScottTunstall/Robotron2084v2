# Code review against `docs/coding-standards.md` (2026-10-11)

Full-repository review of `src/`, `tools/` and `tests/` against the standard in
[coding-standards.md](coding-standards.md). Every finding cites the rule ID from that document.
Findings are ranked most severe first. Format is the one the standard's Part 2 prescribes.

**Baseline:** `dotnet build Robotron2084.slnx -c Debug` — 0 warnings, 0 errors. Working tree
clean apart from this document. No suppressions (`#pragma warning disable`, `[SuppressMessage]`,
`NoWarn`) anywhere. No `...RomTicks` names. No `LifeState` comparisons outside
`EntityLifeExtensions` (STR-9 clean). No `TimeSpan` timers in gameplay code. No
`catch (Exception)` (ERR-1 clean). No `Spec...`/`SpecPixels` identifiers. Test folders mirror
`src/` (TEST-4 clean).

**Method:** the standard's Part 2 procedure — build, mechanical scans over all of `src/`,
`tools/` and `tests/`, then file-by-file reading (`Entities`, `Level`, `AttractMode`,
`Audio`, `Graphics`, `Hud`, `Input`, `Persistence`, `States`, `Palette`, `Tuning`, `Core`,
both tools, the test layout). One extra probe was run, and is the source of the four
documentation findings:

```
dotnet build src/Robotron2084/Robotron2084.csproj --no-incremental -p:GenerateDocumentationFile=true
```

**Relation to the 2026-10-10 review:** the codebase is in good shape and most of that review's
items are still open. Items confirmed **fixed**: the `Unused (kept for the uniform shape)`
wording is gone, no new static mutable state, no new downcasts in entity callbacks, the
`?? new Random()` sites are unchanged, and the `_sprites.Blitter.…` count has fallen from 101
to 86. Items confirmed **still open**: the playtest invincibility Blocker, `IEntity.Update`'s
unused parameters (SOLID-I), the calculated properties (NAM-10), the nested types (STR-3) and
the `?? new Random()` defaults (TIME-3).

**Counts: 1 Blocker, 30 Major, 12 Minor, 5 Notes.**

---

## Blocker

```
[Blocker] TEST-2 + TEST-3 + TEST-1 src/Robotron2084/Tuning/PlayerTuning.cs:83,
  src/Robotron2084/Level/PlayField.cs:109, src/Robotron2084/Entities/Player.cs:110 — the
  playtest invincibility aid is ON in the shipped game. `PlayerTuning
  .PlayerInvincibleForTesting => true`, `PlayField`'s constructor defaults
  `playerInvincibleForTesting` to `true`, and the only production call site
  (States/PlayingState.cs:266) does not pass the argument.
  Why: `Player.Kill()` returns early while `InvincibleForTesting` is on (Player.cs:253), so
  in a real game the player can never be killed, no man is ever lost, and no wave can end.
  Every gameplay feature built on death (the freeze, the wave-clear, the two-player hand-off,
  the high-score entry) is unreachable in the running game. The aid is also a public settable
  property with no "(test hook)" label, and its "TEMPORARY playtest aid … TURN THIS OFF"
  summary names no tracking item (TEST-3). This is the 2026-10-10 review's first Blocker,
  unfixed.
  Fix: set `PlayerTuning.PlayerInvincibleForTesting` to `false`; default the constructor
  parameter to `false`; give the playtest aid a dev key or a settings flag with a tracking
  reference; make `Player.InvincibleForTesting` `internal`, label it "(test hook)" and group
  it with the other test hooks (TEST-1).
```

---

## Major

```
[Major] CMT-8 src/Robotron2084/Robotron2084.csproj (and tests/Robotron2084.Tests) —
  `GenerateDocumentationFile` is not set, so the `CS1591 = error` rule in .editorconfig:23
  can never fire.
  Why: CMT-8 says "`GenerateDocumentationFile` must be on, so the compiler enforces all of
  this". With it off, the build is silent about missing summaries, malformed XML and broken
  `cref`s, and the standard's most-checked rule is unenforced — which is why the four
  findings below exist and have accumulated. Building with the property on reports **592
  undocumented public members, 102 malformed XML comments, 12 unresolved crefs and 8
  param-tag mismatches**. `tools/RobotronSoundPlayer/RobotronSoundPlayer.csproj:9` already
  opts in, so the pattern exists in the repo.
  Fix: add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to
  Directory.Build.props, then clear the four findings below so the set builds green.
```

```
[Major] CMT-8 592 public members with no `<summary>` at all, concentrated in:
  Hud/HighScoreTableLayout.cs (58), Tuning/HudLayout.cs (58), Graphics/SpriteSet.cs (34),
  AttractMode/MovieAnimation.cs (32), AttractMode/MovieDescriptors.cs (32),
  Tuning/CollisionSizes.cs (32), Tuning/PlayerTuning.cs (30), Tuning/ScreenTuning.cs (22),
  Hud/InitialsEntryLayout.cs (20), Tuning/ScoreBurstTuning.cs (20), Tuning/SparkTuning.cs (18),
  Core/Direction8.cs (16), Input/InputAction.cs (16), Tuning/SpheroidTuning.cs (16),
  Tuning/QuarkTuning.cs (14), Tuning/AttractTuning.cs (12), RobotronGame.cs (10),
  States/GameStateManager.cs (10), Tuning/ProgTuning.cs (10), Tuning/SpawnTuning.cs (10),
  and 30 more files.
  Why: CMT-8 requires a summary for every public, internal and private member, enum values
  included. In these files the meaning lives in prose at the top of the class instead:
  Tuning/HudLayout.cs:11-19 explains the screen size above `ArcadeScreenWidth`, and HudLayout
  and HighScoreTableLayout together carry 116 undocumented constants. CMT-15 (a constant that
  changes a field must name it with `<see cref>`) cannot be satisfied without per-constant
  summaries, and a caller reading `HudLayout.HudScoreDigitAdvancePixels` in an editor gets
  nothing.
  Fix: one summary per constant: what it is, its unit (NAM-2), and where it comes from
  (CMT-1/CMT-14). Move the class-level prose into the member that owns it (CMT-10).
```

```
[Major] CMT-8 102 malformed XML comments in 22 files — in 21 of them the pattern is a
  `<remarks>` whose prose paragraph is closed with a stray `</item>` and `</list>`:
  Level/GruntSpeedProgression.cs, Level/WaveMaterialisation.cs, Level/FamilyList.cs,
  Level/TransportImage.cs:25-26, Level/RobotTransporter.cs, Level/LevelParameters.cs,
  Level/PlayerSlot.cs, Level/WaveTable.cs, Level/DifficultyTuning.cs, Level/BozoMode.cs,
  Level/FinalScore.cs, Level/LaserWallFlares.cs, Level/LaserWallFlare.cs,
  Level/RobotKindInfo.cs, Level/LevelParameterGenerator.cs, Level/GameSession.cs,
  Level/ScoreValues.cs, Level/PlayerAppear.cs, Level/WaveSurvivors.cs,
  Level/WaveStartSequence.cs, and Audio/Synthesis/CrowdRoarSound.cs:303, where a `<summary>`
  is never closed.
  Why: the shape is `<list>` … `</list>`, then a prose paragraph, then `</item>` and `</list>`
  with nothing opening them. The generated XML documentation for most of `Level/` is broken,
  so the ROM provenance these remarks record — the reason the remarks exist — is unreadable
  downstream, and the paragraph's own text is at the parser's mercy.
  Fix: keep the prose paragraph inside `<remarks>` after the `</list>`, and delete the
  trailing `</item>`/`</list>`; close the `<summary>` at CrowdRoarSound.cs:303.
```

```
[Major] CMT-8 + NAM-8 6 unresolved `cref` targets: Level/GameSession.cs:57 `Controls`,
  Graphics/BlitterDraw.cs:60 `Overlap`, Graphics/SpriteSet.cs:333 and :341
  `TitleWordmarkCore`, SpriteSet.cs:334 `TitleWordmarkRim`, Program.cs:17 `DisplayInfo`.
  Why: CMT-8 requires crefs to resolve. `TitleWordmarkCore` and `TitleWordmarkRim` are the
  pre-rename names — refactoring-ledger.md records that every `SpriteSet` texture gained
  `Sprite` on the end — so the docs point at members that no longer exist. NAM-8 is explicit
  that a rename is not finished until the old word is gone from crefs too.
  Fix: `TitleWordmarkCoreSprite`, `TitleWordmarkRimSprite`; resolve `Controls`, `Overlap` and
  `DisplayInfo` to the members that exist now.
```

```
[Major] CMT-8 src/Robotron2084/Level/PlayfieldWall.cs:31-32,
  src/Robotron2084/Graphics/WilliamsLogoBorder.cs:87-88 — `<param name="cycle">` where the
  parameter is `colourCycle`, `<param name="logo">` where it is `logoMask`, and no tag for
  either real parameter.
  Why: CMT-8 — "`<param>` tags match the real parameters exactly: no stale tags, none
  missing". The reader is told about a `cycle` that does not exist and given no word about
  the argument they must actually supply.
  Fix: rename the tags to `colourCycle` and `logoMask`.
```

```
[Major] CMT-10 + CMT-11 309 `<summary>` blocks whose first sentence is ROM shorthand rather
  than plain English — 243 in Audio/, 66 in AttractMode/. Examples:
  Audio/Synthesis/CrowdRoarSound.cs:127 `<summary><c>RNT</c>: <c>BSR</c>, <c>LDAA NFRQ2</c>,
  <c>DEC NFRQ</c>, <c>BEQ</c></summary>`; :56, :63, :70, :91, :110, :130, :136, :145, :156
  (the same shape); Audio/Synthesis/BoardMemory.cs:15, 18, 24, 29, 32, 35, 38;
  Audio/SoundSequence.cs:3 ("the (dur, len, note) entries");
  AttractMode/AttractPageMachine.cs:27, 30, 33, 36, 39 ("The ROM's `TOPLEF+$10` = row 48:
  where CLEARM starts clearing"); AttractMode/AttractObjectMachine.cs:29, 92, 156, 572, 651.
  Why: CMT-10 says a `<summary>` names what the thing IS and the mechanism goes in
  `<remarks>`; CMT-11 requires everyday words at about a 10-year-old's reading age and puts
  the ROM cross-references in the `<remarks>` bullets. These summaries are assembly listings:
  a reader who does not already know 6800 mnemonics learns nothing, and the two ROM-source
  bullets CMT-11 asks for are missing from the ones that are pure label dumps.
  Fix: rewrite each summary as one plain sentence ("Starts the crowd roar's random numbers"),
  and move the label, the mnemonics and the address into the `<remarks>` bullets in the form
  the entity classes already use.
```

```
[Major] CMT-4 + CMT-10 class summaries and comments that quote values which live in
  constants: Tuning/HudLayout.cs:21 ("An eight-digit score is 7 pixels longer, so the port
  moves both rows of men 4 columns (8 pixels) to the right"), Hud/HighScoreTableLayout.cs:3-23
  (the whole geometry, with `LDX #$1A35`, "5 per column", "9 rows / 52 columns apart", spelled
  out in prose while all 58 constants below it are undocumented),
  AttractMode/AttractPageMachine.cs:206 ("GRUNTS — 14 of them, one every 16 frames" beside
  `_gruntsLeft = 14; _gruntTimer = 16;`), Level/PlayField.cs:356, :369 ("There may never be more
  than twenty"), Tuning/GorfTuning.cs:18 ("They are all the same height, 16 for now"),
  Level/TransportImage.cs (the remarks restate the byte packing the code does).
  Why: CMT-4 forbids writing a constant's value, a count or a score into a comment: name the
  constant, which cannot go stale. HudLayout.cs:21 has already drifted — the numbers it
  quotes are `HudMenOriginColumnP1`/`P2`, and the "7 pixels" is `HudScoreDigitAdvancePixels`,
  which the sentence never names.
  Fix: name the constants in the prose, and where the prose is the only record of a value,
  turn it into the constant's own summary.
```

```
[Major] CMT-3 + CMT-1 src/Robotron2084/Persistence/GameSettingsStore.cs:7,
  src/Robotron2084/RobotronGame.cs:124, src/Robotron2084/States/SettingsState.cs:25 — three
  comments name the settings file `settings.ini`; the code writes `gameSettings.ini`
  (GameSettingsStore.cs:31).
  Why: a comment that states a false fact is Major by this standard's own severity table. All
  three tell a reader (or a support request) to look at a file that does not exist, and
  `SettingsState`'s sentence ("Every change is written straight to settings.ini") is the one a
  user would trust.
  Fix: say `gameSettings.ini`, or name it through the code in prose
  (`GameSettingsStore.FilePath`).
```

```
[Major] NAM-22 + CMT-1 + FID-1 20 sites cite "the spec", "spec-stated" or "spec.txt" as the
  source of a value: Tuning/PlayerTuning.cs:68, 86, 88; Tuning/CollisionSizes.cs:12, 76, 92,
  95, 101, 106; Tuning/SpawnTuning.cs:39, 41; Tuning/SpheroidTuning.cs:21;
  Tuning/WavePaletteTables.cs:17; Tuning/HudLayout.cs:38; Graphics/SpriteFactory.cs:12;
  Level/PlayField.cs:290, 314; Palette/WallColorCycle.cs:7; States/IGameState.cs:10;
  States/TitleScreenState.cs:201.
  Why: "spec pixel" and `spec-...` are in the glossary's retired-words table because
  `spec.txt` is not in the repository. A "spec-stated" cross-reference cannot be checked
  against anything, and FID-1 says the arcade is the specification — so these comments record
  a provenance that does not exist and invite the next change to trust an unreachable
  document.
  Fix: cite the ROM routine and address (CMT-1) where the arcade is the source, or say "the
  author's tuning" where it is not. Delete "spec" and add it to the retired-words table
  (NAM-22).
```

```
[Major] SOLID-I 27 `<param>` tags documenting parameters that the method ignores, in 20
  entity files: BerzerkRobot.cs:140, Brain.cs:365, CruiseMissile.cs:171, Electrode.cs:88-89,
  Enforcer.cs:145, Gorf.cs:153, Grunt.cs:154, Hulk.cs:361, Human.cs:257, Player.cs:181,
  PlayerLaser.cs:81, Prog.cs:348, Quark.cs:214, RescueScoreMarker.cs:83-84, ScoreBurst.cs:133-134,
  SkullMarker.cs:75-76, Spark.cs:151, Spheroid.cs:329, StripEffect.cs:207-208, Tank.cs:308,
  TankShell.cs:141-142 — all "Not used." on `Update(GameTime, PlayField)`.
  Why: SOLID-I — "Parameters documented as 'Unused (kept for the uniform shape)' are not
  allowed. Delete them", and "a type whose `Update(GameTime, PlayField)` uses neither argument
  … is in the wrong interface". Nearly the whole entity layer is in the wrong shape; the
  2026-10-10 review reported this and only the wording changed.
  Fix: split the interface — a tick-driven `Update()` for the entities that count their own
  ticks, and pass the `PlayField` only to the ones that ask it questions.
```

```
[Major] STR-11 86 second-dot calls in 30 files — `_sprites.Blitter.DrawSprite(...)`,
  `.Blitter.GetSlotColour(...)`, `.Blitter.DrawSolidRectangle(...)`, `.Blitter.UsePassThrough()`
  (every entity's `Draw`), plus 40 `.TextRenderer.` calls. Worst: Prog.cs (5), Human.cs (4),
  ScoreBurst.cs (4), Player.cs (3), Brain.cs (3), CruiseMissile.cs (3).
  Why: the standard names `sprites.Blitter.DrawSprite(...)` as the example of the smell: "If
  a statement has a second dot after a property or a call, it is a smell." Every entity knows
  the structure inside `SpriteSet`, so the F03 `SpriteSet` split in docs/code-review-issues.md
  touches every entity file. Down from 101 in the 2026-10-10 review, so the direction is right.
  Fix: thin delegating methods on the near object (`sprites.DrawSprite(...)`,
  `sprites.GetSlotColour(...)`) — the standard says paying that price is correct — or pass
  `BlitterDraw` into the entity's constructor.
```

```
[Major] STR-3 13 nested types in src/ and tools/, and 5 more in tests/:
  AttractMode/AttractObjectMachine.cs:181 `MovieProcess`; Audio/Synthesis/FilteredNoise.cs:283
  `Settings`; Core/DisplayInfo.cs:41 `Rect`; Entities/Prog.cs:535 `Ghost`;
  Graphics/TunnelEffect.cs:370 `Ring`; Level/BozoMode.cs:95 `Row`;
  Level/DifficultyTuning.cs:281 `Header`; Level/LevelParameterGenerator.cs:118 `LevelTableRow`;
  Palette/HighScorePalette.cs:175 `Process`; Palette/PaletteAnimator.cs:120 `Process`;
  Persistence/HighScoreStore.cs:77 `SavedTable`; States/StorylineState.cs:216
  `MovieExplosionSource`; tools/SpriteExtractor/Program.cs:495 `Rgb`;
  tests/…/PlayerAnimationAndAimTests.cs:159 and PlayFieldBrainProgMissileTests.cs:829
  `PhaseInput` (the same helper twice), StripEffectTests.cs:568 `FakeDead`,
  DemoPlayerInputSourceTests.cs:170 `MutableStick`, PixelCollisionTests.cs:92 `StubPixelCollision`.
  Why: STR-3 — "no nested types … pull it out and give it its own file". A nested type is
  invisible to a name search, and `Process` now names two unrelated types (NAM-9: one name,
  one meaning). The duplicated `PhaseInput` is also TEST-5's "shared setup goes in a builder,
  not a private `CreateField` copied into each test file".
  Fix: one file per type, `internal` where it should not be public; `HighScorePaletteProcess`
  and `PaletteAnimatorProcess`; one shared `tests/…/FakePlayerInputSource`.
```

```
[Major] STR-1a 11 files over the 500-line hard cap: Level/PlayField.cs 887,
  Entities/Brain.cs 725, AttractMode/AttractObjectMachine.cs 675, Entities/Spheroid.cs 598,
  Entities/Hulk.cs 580, Audio/Synthesis/WaveTableSound.cs 549, Entities/Player.cs 540,
  Entities/StripEffect.cs 542, Entities/Prog.cs 536, Entities/Tank.cs 504,
  tools/SpriteExtractor/Program.cs 886.
  Why: STR-1a is a hard cap. Each has taken a second job: `PlayField` orchestrates, scores,
  sounds, spawns, prunes and draws the wave start in one file; `AttractObjectMachine` is an
  opcode interpreter, a walker and an explosion source.
  Fix: find the second job and move it out (STR-1) with its own file (STR-3).
  `WaveMaterialisation`, `WaveStartSequence` and `CollisionResponder` are the shape to copy.
```

```
[Major] STR-2a src/Robotron2084/Graphics/SpriteSet.cs:111-173 (62 lines) and
  tools/SpriteExtractor/Program.cs:53 `Main` (222 lines), :376 `ExtractOne` (61), :531
  `CollectData` (53).
  Why: STR-2a is a hard cap. The `SpriteSet` constructor is 29 near-identical
  `source.LoadAll(...)` lines, and the interesting fact in it — how many animation frames each
  character has — is spread over the whole wall and duplicated in the class remarks.
  Fix: a data table of (property, path, frame count) rows loaded in a short loop, and split
  `Main` into argument parsing, ROM location and extraction.
```

```
[Major] STR-2 + STR-2b src/Robotron2084/Level/PlayField.cs:298, 301, 308, 314 and
  src/Robotron2084/RobotronGame.cs:60, 68 — bodies divided by numbered comment headers
  ("// 1. Wall.", "// 2-4. …", "// 8. Player — ALWAYS last"). Section divider comments divide
  seven more: AttractMode/AttractObjectMachine.cs:231, Graphics/SpriteFactory.cs:107, 174,
  Hud/HighScoreTableLayout.cs:54, Hud/InitialsEntryLayout.cs:22, 46, 53.
  Why: STR-2 — "a method whose body needs comment headers ('// 1. …', '// 2. …') is several
  methods"; STR-1 names `// ---- section ----` dividers as the sign of a class with more than
  one job. `PlayField.Draw`'s headers are a method list written in comments instead of calls.
  Fix: extract `DrawWallAndFlares`, `DrawFamilyAndRobots`, `DrawShots`, `DrawExplosions`,
  `DrawPlayer` and let the body read as that list; split the divided classes.
```

```
[Major] NUM-1 magic numbers. Graphics/SpriteSet.cs:115-120, 153-156 —
  `GetNumberedNames("Sprites/Gorf", 2)`, `"Sprites/BerzerkRobot_Idle", 6)`,
  `… "_WalkRight", 2)`, `… "_WalkUp", 3)`, `… "_WalkDown", 3)`, `source.Create(6, 1, …)`,
  `source.Create(2, 6, …)`, `source.Create(6, 6, …)`, while every other character's count is a
  named constant and the laser's size already is one (`SpriteFactory.LaserDiagonalSize`,
  private, SpriteFactory.cs:29).
  AttractMode/AttractPageMachine.cs:76 (`TextSlot = 10`), :94 (`opcode <= 9`), :100
  (`opcode >= 0x5F`), :109 and :267 (`_waitRomFrames = 3`), :207-208 and :267
  (`_gruntsLeft = 14`, `_gruntTimer = 16` — the same pair in two places), :239
  (`_cursorY += 11`), :232 (`range - 0x10`).
  Level/LevelParameters.cs:38-50 — the record's defaults (`MaxDropsX2 = 10`,
  `MaxEnforcersPerSpheroid = 5`, `GruntMoveDelay = 15`, `EnforcerFireDelay = 24`,
  `BrainFireDelay = 40`, `TankFireDelay = 32`, …).
  Entities/Grunt.cs:245 (`int stepBeats = 4`) and :233 (`MoveDelayBeats * 7 / 8`).
  Palette/GamePalette.cs:85 (`for (var slot = 10; slot <= 15; slot++)`),
  Graphics/SpriteFactory.cs:168, 184 (`<= 13`, `13 * 13`), Hud/ArcadeHud.cs:118 (`wave >= 10`).
  Why: NUM-1 — every literal other than 0, 1, -1 (and 2 halving) is a named constant whose
  summary says what it is and where it comes from. `TextSlot`'s 10 is a palette slot
  (`PresentationPagePalette.TextSlot` is the same 6 in the palette folder); the 10..15 loop is
  the flashing slots the glossary defines; the doubled 14/16 pair in AttractPageMachine can
  drift apart.
  Fix: named constants in `Tuning/` (or beside the row that uses them), one per meaning;
  `GamePalette.FlashingSlotFirst`/`FlashingSlotLast`; `GruntSpeedUpNumerator`/`Denominator`.
```

```
[Major] NUM-4 20 sites of raw subpixel arithmetic instead of the named scale:
  AttractMode/AttractObjectMachine.cs:99, 153, 163, 164, 298, 299, 434, 435, 655;
  AttractMode/AttractPageMachine.cs:161; AttractMode/MovieObject.cs:80, 86;
  Entities/StripEffect.cs:238, 399, 405, 484; Level/DifficultyTuning.cs:266.
  Why: NUM-4 — conversions go through named helpers, and the subpixel scale is named once
  (`ScreenSize.SubpixelsPerPixel`, Core/ScreenSize.cs:59), as Enforcer.cs:238, Quark.cs:334,
  Spark.cs:187 and TankShell.cs:341 all do. A bare `>> 8` means a column in
  AttractObjectMachine, a strip gap in StripEffect and a fixed-point product in
  DifficultyTuning — three meanings, one invisible constant. MovieObject.cs:12-13 has to write
  the arithmetic out in prose to be readable at all.
  Fix: `ScreenSize.SubpixelsPerPixel` (and `SubpixelBits` for a shift) at each site, as the
  entities already do.
```

```
[Major] NUM-7 56 public ROM tables exposed as writable arrays: Level/WaveTable.cs (24
  tables), AttractMode/AttractMovieData.cs (7), Level/RobotKinds.cs:33 `All`,
  Level/Collisions/CollisionRules.cs:12 `InArcadeOrder`, Graphics/SpriteContentPaths.cs:10
  `All`, Input/InputActions.cs:7 `All`, AttractMode/MovieDescriptors.cs:28 `All`,
  Tuning/PlayerTuning.cs:70, 73, and Level/TransportImage.cs:74 `Template`.
  Why: NUM-7 — "a `static readonly int[]` can still be written to by any caller". A caller can
  write `WaveTable.Grunts[0] = 99` and change every wave; `RobotKinds.All` and
  `CollisionRules.InArcadeOrder` are the registries STR-6 calls the single source of truth, and
  they are the most exposed. `TransportImage.Template` additionally hands out the animation
  frame's pixels to any caller.
  Fix: expose `IReadOnlyList<T>` (or `ImmutableArray<T>`), keeping the array private.
```

```
[Major] TIME-3 src/Robotron2084/Entities/BerzerkRobot.cs:95, Grunt.cs:106, Player.cs:95,
  Input/DemoPlayerInputSource.cs:51, Palette/PaletteAnimator.cs:56, Palette/TunnelPalette.cs:132
  — `random ?? new Random()` constructor defaults, each documented in its `<param>` tag as "If
  this is null, the grunt makes its own".
  Why: TIME-3 forbids exactly this default. A gameplay class that makes its own random source
  is unrepeatable, and a test that forgets the argument silently stops being seeded — the
  fidelity tests (FID-3) that pin random rolls then pass by luck. Unchanged since 2026-10-10.
  Fix: make the `Random` parameter required. RobotronGame.cs:209 and TitleScreenState.cs:255
  are where entropy legitimately enters.
```

```
[Major] NAM-12 19 declarations and 75 occurrences of the American `color` in names we own:
  Graphics/BlitterDraw.cs:148, 175, 225, 227 (`Color color`, `<param name="color">`),
  Graphics/ContentSpriteSource.cs:32, Graphics/ISpriteSource.cs:22-23,
  Graphics/SpriteFactory.cs (the `color` parameter of every `Build…Pattern` and `FillRect`),
  Graphics/SpriteMask.cs, Graphics/WilliamsLogoBorder.cs.
  Why: NAM-12 keeps a framework member's spelling (MonoGame's `Color`, `Rectangle.Center`) and
  requires British spelling in ours, with the exact example "`SlotColour` returning a `Color`".
  `BlitterDraw.GetSlotColour` (BlitterDraw.cs:241) is British while the parameter of the method
  beside it is `color` — the same file spells one word two ways (CON-2).
  Fix: rename the parameters and locals to `colour`, and the `<param>` tags and `<paramref>`
  references in the same change (NAM-8).
```

```
[Major] NAM-12 src/Robotron2084/Audio/Sound.cs:18, 44 — `Sound.Initialize`.
  Why: our own method, not a framework member, and NAM-12 lists `initialise`.
  Fix: `Initialise`, with its call sites, tests and the cref at Sound.cs:18 (NAM-8).
```

```
[Major] CMT-13 "picture", "image" and bare "frame" for a character's drawing:
  Graphics/LetterSpan.cs:3-4 ("a picture of a word", "in the picture's own pixels"),
  Graphics/WordmarkAppear.cs:25, 52, 79 ("the same picture", "Where the whole picture is
  drawn"), Level/TransportImage.cs:167 (`bytesInPicture`), Level/RobotTransporter.cs:31, 43,
  46, 66 ("each image", "the image it shows", the tuple element `(IEntity Robot, TransportImage
  Image)`), AttractMode/AttractMovieData.cs:196 and AttractMode/MovieWalk.cs:6 ("image x4"),
  AttractMode/AttractObjectMachine.cs:255, 321, 447 ("descriptor, image", "frames per image",
  "image colour"), AttractMode/MovieAnimationFrames.cs:26-27 ("ROM image 2 is the same sprite
  as image 0").
  Why: CMT-13 bans "picture", "art" and "image" for a drawing, and bare "frame" (which means a
  ROM frame); the words are "animation frame" and "sprite". "picture" is also in the glossary's
  retired-words table, and `bytesInPicture` is an identifier we own, not a quoted ROM label.
  Fix: `bytesInAnimationFrame`, "the sprite's own pixels", "Where the whole wordmark is drawn",
  "(animation frame x4, dx pixels, dy pixels)", and name the tuple element `Frame`.
```

```
[Major] NAM-14 src/Robotron2084/Audio/SoundTables.cs:94, 205 — the electrode's sound table is
  `PostKill`; Palette/WavePaletteTables.cs:47, 57, 67 write "the POST IMAGE offset table",
  "The POST COLOUR per wave" and "make that wave's posts cycle".
  Why: NAM-14 is explicit — "It is an electrode, never a post … 'Post' appears only inside a
  quoted ROM label or a comment that says which ROM routine it maps to". `PostKill` is an
  identifier we own, and "that wave's posts cycle" is our own sentence with no ROM label in it.
  The same file's siblings are already `SoundTables.Laser`, `.RobotHit`, `.CircleKill`.
  Fix: `SoundTables.ElectrodeKill`, its summary quoting `PSTKIL` "POST KILL" (CMT-1), and
  "electrode" in the WavePaletteTables prose.
```

```
[Major] Glossary (transporter) + NAM-9 + NAM-22
  src/Robotron2084/Audio/TransporterSound.cs:22, 47, 53, 64, 73 and
  tools/RobotronSoundPlayer/TransporterRequest.cs:23, 30 — the summaries call the effect "the
  warp-in" ("The warp-in sound number", "Starts the warp-in").
  Why: the glossary lists transporter / transport with "warp-in" in the don't-say column, and
  the terminology ledger records that "warp in" is robomame.asm's paraphrase while the original
  source (`RRT2.ASM`, titled `TRANSPORTER`) says transporter. The class names are right and
  their own docs use the retired word (CON-2).
  Fix: "the transport", "Starts the transport: …", "the transporter's beam-in hum". The
  `TeleportTo` test hooks (Player.cs:283, Hulk.cs:449) are the idiom TEST-1 prescribes and are
  not part of this finding.
```

```
[Major] Glossary (family member) + NAM-15 "human" in prose:
  Level/FieldEntities.cs:81, 87, 97 ("The progs: humans the brains have reprogrammed",
  "where humans were rescued", "where humans were killed"), Level/PlayField.cs:165, 585,
  Level/ScoreValues.cs:87, and the test names
  tests/…/Level/PlayFieldBrainProgMissileTests.cs:187 (`Brain_StepsTowardNearestHuman_…`) and
  :214 (`Brain_WithNoLivingHumans_ChasesThePlayer`).
  Why: the glossary's word for the group is family member, "in names, comments and docs
  alike", and "human (in prose)" is in the retired-words table. NAM-15 extends the rule to test
  names. The class `Human` keeps its name by the author's decision, so the prose is the part
  that is out of line.
  Fix: "family members" in the summaries and `…NearestFamilyMember…` / `…WithNoLivingFamilyMembers…`
  in the test names.
```

```
[Major] DEAD-2 src/Robotron2084/Level/GameMode.cs:28-30,
  src/Robotron2084/RobotronGame.cs:264-266 — `TwoPlayerSimultaneous` is selectable (F3 on the
  title screen) and its summary says "It is not built yet, so it plays as
  <see cref="TwoPlayerAlternate" />".
  Why: DEAD-2 — "Selectable features that are not built must not be selectable." The player can
  pick a mode that silently does something else, with no word on screen; and the "not built
  yet" text is CMT-10's case that must name a tracking item, which it does not.
  Fix: remove the F3 binding (and the enum value) until the mode exists, or gate it behind a
  dev-only switch that names its tracking item (TEST-3).
```

```
[Major] NAM-10 properties that compute, look up, compare or allocate:
  Audio/Synthesis/SoundBoard.cs:147 `IsPlaying`, Audio/Sound.cs:40 `IsPlaying`,
  Entities/EntityList.cs:61 `Count => _items.Count`, Entities/ScoreBurst.cs:109 `Position`,
  Entities/StripEffect.cs:166 `Position`, States/StorylineState.cs:243 `Position`,
  tools/RobotronSoundPlayer/CommandLine.cs:20 `Command`, :23 `Choice`,
  tools/RobotronSoundPlayer/PlaybackLength.cs:35 `FellSilent`, :38 `ReachedLimit`, :41
  `HeardSeconds`.
  Why: NAM-10 — a bool worked out from other members, a lookup into another object and a
  per-call allocation are methods, not properties. The three `Position` properties each build a
  `new IntVector2(...)` on every read, which is the allocation case the rule names; `Sound.IsPlaying`
  hides a null-conditional lookup through the engine behind a property. Unchanged since
  2026-10-10.
  Fix: `IsPlaying()`, `GetCount()`, `GetPosition()`, `GetCommand()`, `GetChoice()`,
  `HasFallenSilent()`, `HasReachedLimit()`, `GetHeardSeconds()`.
```

```
[Major] SOLID-L src/Robotron2084/Level/Collisions/CollisionResponder.cs:24-37 — nine casts of
  the base `CollisionResult` to a concrete result type inside a `typeof`-keyed dictionary of
  callbacks.
  Why: SOLID-L — "No downcasts from an interface to a concrete type in callbacks … Where that
  is not possible, pattern-match and throw an `InvalidOperationException` that names the type."
  A new rule that reports a result the table has no entry for fails with a cast exception naming
  nothing, not a message saying which responder is missing.
  Fix: pattern-match each result (`result is LaserHitResult laser => …`) and make the missing
  case throw an `InvalidOperationException` naming the result type.
```

```
[Major] NAM-21 relative names that never say what they are relative to:
  AttractMode/MovieObject.cs:74 `ShakeRowOffset`, Audio/Synthesis/SoundBoard.cs:46, 52, 55
  `LowWaveTableOffset` / `HighWaveTableOffset` / `SquareWaveOffset`,
  Audio/Synthesis/WaveTableSound.cs:214 `_pitchOffset`, Entities/Brain.cs:131
  `VictimRightOffsetColumns`, Entities/Player.cs:297 `GetMuzzleOffset`, Entities/Prog.cs:61-98
  `OffsetXHalfRange`, `OffsetXRollMax`, `OffsetXStepColumns`, `OffsetYCentre`,
  `OffsetYStepRows`, `OffsetYSteps`, `_offsetX`, `_offsetY`, Hud/HighScoreTableLayout.cs:20
  ("the score at a FIXED offset from the post-rank cursor").
  Why: NAM-21 requires both ends — what it is an offset of, and what it is an offset from.
  `VictimRightOffsetColumns` = 8: right of the brain, of the victim, of the playfield?
  `GetMuzzleOffset` returns an offset from the player's position, which the name does not say.
  `Prog._offsetX` is an offset from the player, which only the summary reveals.
  Fix: `VictimRightOffsetFromBrainColumns`, `GetMuzzleOffsetFromPlayer(direction)`,
  `AimOffsetXFromPlayerColumns`, `ShakeRowOffsetFromObjectRow`,
  `PitchOffsetFromPatternStart`; where the name would be unreasonable, say both ends in the
  summary (NAM-21's second half).
```

```
[Major] CMT-3 history written into code:
  Core/ArcadeClock.cs:25 ("See notes §52 for the derivation and the bug (entities running 17-20%
  fast) that this pattern replaced"), Tuning/HudLayout.cs:40 ("which is exactly the round-8
  complaint (\"the score also overlaps the border wall\")"),
  Tuning/PlayerTuning.cs:84 ("flip to false when playtesting is done"),
  Hud/HighScorePageHold.cs:8 ("which are NOT what the port first shipped"),
  Tuning/GorfTuning.cs:18 ("16 for now"), States/PlayingState.cs:179 ("the second input … is
  not built yet").
  Why: CMT-3 — no dates, playtest rounds, "used to", "was wrong", no history in code; the code
  says what is true now. CMT-10 — an "open item" text must name its tracking row, and five of
  these name nothing.
  Fix: state the present fact ("A hulk's step lands 17-20% fast if the tick is used as the
  frame"), delete the round reference, and give each unfinished item its handoff/notes row.
```

---

## Minor

```
[Minor] NAM-19 bool fields that do not read as predicates:
  Entities/Brain.cs:228 `_reprogramMovingDown`, Level/PlayField.cs:61 `_tankShellBugEnabled`,
  Level/WaveMaterialisation.cs:168 `_transportBegun`, States/HighScoreTableState.cs:61
  `_rampsStarted`, States/PlayingState.cs:53 `_restartHandled`, States/WaveClearState.cs:49
  `_paletteStarted`.
  Why: NAM-19 requires a boolean to read as a question in every kind of name.
  `_transportBegun` reads as a noun, and PlayingState.cs:53's comment ("one-shot so the death
  branch fires exactly once") is doing the name's job.
  Fix: `_isReprogramMovingDown`, `_isTankShellBugOn`, `_hasTransportBegun`,
  `_haveRampsStarted`, `_hasRestartBeenHandled`, `_hasPaletteStarted`.
```

```
[Minor] NAM-6 single letters as ordinary method parameters:
  Level/PlayField.cs:238 `Touches(IEntity a, IEntity b)`, Graphics/SpriteMask.cs:102
  `GetSharedScreenRect(Rectangle a, Rectangle b)`, Core/IntVector2.cs:47
  `IsFartherThan(IntVector2 b, int distance)`.
  Why: NAM-6 allows single letters only as loop indices and one-line LINQ parameters. (The
  `operator +(IntVector2 a, IntVector2 b)` overloads follow the framework's convention and are
  not part of this finding.)
  Fix: `Touches(IEntity first, IEntity second)`, `GetSharedScreenRect(Rectangle one, Rectangle
  two)`, `IsFartherThan(IntVector2 other, int distance)`.
```

```
[Minor] Glossary (retired words: "X and Y, axis (in a comment)") + CMT-11:
  Core/Direction8Extensions.cs:5, 11, 43 ("per-axis signs"), Input/PlayerControls.cs:86
  ("-1/0/1 per axis"), Graphics/Presentation.cs:22, 43 ("the wider axis", "the limiting axis"),
  Tuning/SparkTuning.cs:37, 40, 52, 54, Tuning/SpawnTuning.cs:20 ("per-axis random"),
  Level/LaserWallFlares.cs:86 ("the travel axis"), Level/PlayField.cs:646 ("the explosion's
  axis"), Entities/StripEffect.cs:88, 244, 286, 314 (`<param name="axis">`),
  Input/ControlSettings.cs:26 ("a trigger is an axis"), and the test name
  PlayFieldBrainProgMissileTests.cs:238 (`Brain_XApproachHasADeadZone_ButYDoesNot`).
  Why: the retired-words table says to write "left or right, up or down, sideways" in a comment,
  and CMT-11 asks for a 10-year-old's reading age. `StripFanAxis` as a type name is fine; the
  prose is where the word is retired.
  Fix: "one step left/right and one step up/down", "the direction it is travelling", "sideways
  or up and down", `Brain_SidewaysApproachHasADeadZone_ButUpAndDownDoesNot`.
```

```
[Minor] CMT-2 comments that restate the line under them:
  Level/PlayField.cs:290 ("// Prune: remove Dead entries from every list (pruning = count
  decrement, spec)." over `Entities.PruneDead();`), Entities/Grunt.cs:107 ("The grunt waits
  a random number of beats before its first step." over the `_random.Next` line),
  Entities/Grunt.cs:179-181 (the walk-frame comment beside the `%` expression).
  Why: CMT-2 — a comment must not restate the line below it. The PlayField one also carries the
  retired "spec".
  Fix: delete them; where the reason is the point, move it to the member's summary
  (CMT-10).
```

```
[Minor] CMT-8 + FMT-1 Entities/Grunt.cs:85-87 — the field `_walkAnimationFrameNumber` is
  documented by an inline `//` comment, and the declaration is folded over three lines.
  Why: CMT-8 requires a `<summary>` for private fields too; FMT-1 wants a `///` comment on its
  own line above the member.
  Fix: a `<summary>` above the declaration ("Which walk animation frame is showing. The first
  is number 1, as in the arcade."), and the declaration on one line.
```

```
[Minor] NAM-19 Level/TransportImage.cs:74 `Template` (a `Color[]`),
  AttractMode/MovieObject.cs:74 `ShakeRowOffset` (a plain settable `int`),
  Entities/Brain.cs:306 `internal Human? Target` (the family member being chased, and the
  property is `internal` between public members).
  Why: NAM-19 — a name tells you what it holds ("The animation frame's own pixels" is the
  summary of a thing named `Template`), and STR-5 orders members by visibility, so an
  `internal` property must not sit among the public ones.
  Fix: `AnimationFramePixels` (as `IReadOnlyList<Color>`, NUM-7), `ShakeRowOffsetFromObjectRow`,
  `ChasedFamilyMember`, moved to the internal group.
```

```
[Minor] NAM-9 Palette/HighScorePalette.cs:175 and Palette/PaletteAnimator.cs:120 — two
  unrelated private classes both named `Process`.
  Why: NAM-9 — a reader who has seen a name once should predict what the same name does
  elsewhere. (Also STR-3.)
  Fix: `HighScorePaletteProcess`, `PaletteAnimatorProcess`.
```

```
[Minor] TEST-5 tests/…/Entities/PlayerAnimationAndAimTests.cs:159 and
  tests/…/Level/PlayFieldBrainProgMissileTests.cs:829 — the same `PhaseInput` stub written twice.
  Why: TEST-5 — shared setup goes in a builder, not a private helper copied into each test file.
  Fix: one `FakePlayerInputSource` beside `FakeInputSource.cs` in tests/Robotron2084.Tests/.
```

```
[Minor] NUM-1 + CON-1 AttractMode/AttractPageMachine.cs:94, 102, 206-208, 222-260 — the page
  script's opcodes are bare numbers (`case 4: // SNOOZE`, `case 6: // DONE`, `case 8: // GRUNTS`,
  `opcode <= 9`, `opcode >= 0x5F`) with the ROM's label in the comment.
  Why: NUM-1 wants a named constant; the ROM labels stay in the summary as CMT-1 requires, and
  the numbers should carry them (`OpcodeSnooze = 4`). `AttractObjectMachine.cs:231` already
  names its opcode families in a divider comment, so the two interpreters differ (CON-1).
  Fix: one `private const byte Opcode… = n;` per opcode, in both machines.
```

```
[Minor] CMT-16 Level/PlayField.cs:16-56 — the class summary and remarks narrate MonoGame's
  fixed time step, `RobotronGame`'s wiring and the state manager's route to `Update`.
  Why: CMT-16 — an orchestrator documents its own part (what it calls, when, in what order) and
  must not describe the machinery it runs; the summary is also eight sentences, against CMT-11's
  one or two.
  Fix: keep "each tick, in order: …" and the freeze/appear exceptions; move the MonoGame and
  `RobotronGame` account to `RobotronGame`'s own docs.
```

```
[Minor] NAM-13 Entities/Grunt.cs:112 `MoveDelayBeats` (a property) vs Grunt.cs:245
  `WaveSpeedTick(int floorBeats, int stepBeats = 4)`; Level/PlayField.cs:224 `TouchesPlayer`,
  :225 `Touches` — a name that is a noun phrase where the question is the point.
  Why: NAM-19 wants a method returning `bool` to read as a question, and NAM-18 wants a method
  name not to repeat its class. `WaveSpeedTick` says the mechanism (a tick) rather than the
  effect, and sits beside `SpeedUp` on the same class (NAM-7: near-synonyms must mean different
  things, and here two speed-up rules differ only in an unexplained word).
  Fix: `SpeedUpOnWaveProgress(stepBeats, floorBeats)`; `TouchesPlayer(entity)` is fine, keep it.
```

```
[Minor] NAM-2 + NAM-19 Level/PlayerSlot.cs:31 `record PlayerSlot(int Number, …, int Lives, int
  Wave)` and the mutable `public int Wave { get; set; }` re-declared at :40 with a summary
  duplicating the record's `<param>` at :28.
  Why: the same fact is documented twice (CMT-2's habit in docs form), and the re-declaration
  exists only to attach docs, which a reader reads as a behaviour change.
  Fix: document once — keep the `<param>` tag, delete the duplicated property summary, and note
  in one sentence that `Wave` moves on only through this player.
```

---

## Notes

- **DEAD-1 / PROC-2 — tools/SpriteExtractor/** is not in `Robotron2084.slnx` (the solution lists
  `Robotron2084`, the two test projects and `RobotronSoundPlayer`). Nothing builds it, so
  `dotnet build` and `dotnet test` never see its 886-line `Program.cs`, its nested `Rgb` type,
  its 222-line `Main` or its magic numbers. It is a real tool (it produced `docs/sprite-map.md`),
  so it is not dead code — it is unbuilt code. Add it to the solution, or say in its header that
  it is a developer-only script deliberately outside the build.
- **SOLID-D — `Sound.Enabled`, `Sound.AttractMuted`** (Audio/Sound.cs:30, 37) and
  **`DevKeys.AttractFastForward`** (Input/DevKeys.cs:48) are static mutable state. The standard
  names them as "the existing ones to shrink, not grow", and no new statics were found.
- **Glossary — `Human` / `HumanKind`** keep the old word by the author's decision
  (glossary.md). Not flagged; only the prose in the Major entry above is.
- **STR-1a in tests** — `tests/…/Level/PlayFieldBrainProgMissileTests.cs` (838 lines) and
  `tests/…/Entities/StripEffectTests.cs` (606) are over the cap. The standard's 500-line scan
  covers `src/` and `tools/`, so this is a note, not a finding; splitting them by behaviour
  would help the same way.
- **FID-4a** — Gorf (`Entities/Gorf.cs`, `GorfPath`, `GorfTuning`) and the Berzerk robot
  (`Entities/BerzerkRobot.cs`) say they are the author's robots where it matters
  (Grunt.cs:214, Gorf.cs:20). No fidelity test pins them to a ROM value. Clean.

---

## Summary

**1 Blocker, 30 Major, 12 Minor, 5 Notes.**

The most important finding is the Blocker: the shipped game runs with the playtest
invincibility aid on, so the player cannot be killed and no wave can end. The largest systemic
item is the documentation toolchain: `GenerateDocumentationFile` is off, so the compiler never
checks the rule the standard relies on most — 592 public members have no summary, 102 XML
comments are malformed, 6 `cref`s point at members that no longer exist, and 309 summaries are
assembly listings instead of plain English. The pattern is consistent: where the compiler can
see a rule, this codebase is clean (no suppressions, no clock literals, no column literals, no
broad catches, no `LifeState` comparisons outside their owner, no `...RomTicks`, tests mirror
`src/`). Where it cannot — comments, summaries, unit discipline in prose, retired vocabulary —
the findings concentrate. The vocabulary items (`spec`, `post`, `warp-in`, `human`, `picture`,
`color`, `axis`) are all cheap, mechanical fixes; the structural ones (`PlayField` at 887 lines,
`IEntity.Update`'s unused parameters, the 86 `sprites.Blitter.…` calls) need the author's
decision on interface shape.

Questions for the author:

1. Should `GenerateDocumentationFile` go into `Directory.Build.props` now, accepting that the
   592 summaries must be written before the set builds green — and in which folder order?
2. Is the `IEntity.Update` split (SOLID-I) approved as a tick-driven `Update()` plus a
   field-taking interface, as the 2026-10-10 review asked?
3. `GameMode.TwoPlayerSimultaneous`: remove the F3 binding, or keep it as a documented
   dev-only switch with a tracking row?
4. Is `PlayerTuning.PlayerInvincibleForTesting` still wanted for playtesting? If so, on which
   dev key, and where is it tracked?
