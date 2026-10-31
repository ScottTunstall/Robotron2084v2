# Naming: identifiers that do not say what they hold (2026-10-11)

This is a work list, in the shape of [code-review-issues.md](code-review-issues.md), for one kind of
finding only: **parameters, fields, properties and (where a fix has to include them) local variables
whose names do not tell the reader what they are.**

The pattern that started it: `ButtonEdgeDetector.Advance(PlayerInputState now)`. `now` reads as a
moment in time; the value is one poll of the input. The author renamed it to `currentInputState`
(staged in the working tree on 2026-10-11, alongside `ArcadeRandom`'s `ByteSides` → `MaxValue`,
commit `a194193`). The same shape appears in the four pages that read `InputSnapshot` — they all call
the poll `now` — and in some 250 declarations across the solution, which is what this list is for.

The standard being applied is §3 of [coding-standards.md](coding-standards.md) (NAM-1 to NAM-22),
with [glossary.md](glossary.md) for the words and unit suffixes. Every item names the rule it breaks.

## How this list was made

Every parameter, field and property in `src/`, `tools/` and `tests/` was extracted mechanically
(5,185 declarations: 2,576 parameters, 2,276 fields, 333 properties) and the names were filtered
against the NAM rules: a name that reads as a time, a count, a collection or an object when the type
is something else; a name that says nothing; one name used for several types; a number with no unit;
a boolean that is not a predicate; an abbreviation. Each surviving item was then read in place before
it was written down, so the suggested name is checked against what the code actually does with the
value.

Section H lists what was looked at and deliberately **not** made into an item, so nobody re-reviews it.

---

## 0. Rules for whoever fixes these

1. **A rename and nothing else.** No behaviour, no signature change, no reordering, no formatting of
   untouched lines. If a name is only fixable by changing what the code does, stop and report.
2. **One item per commit.** Put the item ID in the message, e.g. `N01: the input poll is currentInput, not now`.
3. **NAM-8: the old name goes everywhere** — parameters, fields, properties, locals, `<param>` and
   `<summary>` text, `<see cref>` links, test names, and any doc that is not dated. Search the whole
   solution, `tests/` included, after every rename.
4. **A `<summary>` that carried the meaning is now carried by the name.** Where an item says "the
   comment has to say it in full", the comment stays and gets the new name; do not delete it.
5. **NAM-22:** if a rename retires a word, add the word to the glossary's "Retired words" table in the
   same commit.
6. **After every item, all three must pass:**
   ```
   dotnet build Robotron2084.slnx -c Debug
   dotnet build Robotron2084.slnx -c Release
   dotnet test Robotron2084.slnx
   ```
   0 warnings (warnings are errors), 0 failed tests. On this machine `dotnet test`'s
   Microsoft.Testing.Platform shim cannot launch the compiled apphost ("Zero tests ran", exit 5);
   running the assembly directly works — `dotnet tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.dll`
   (808 tests, 1 skipped for the missing ROM dump) and the same for `RobotronSoundPlayer.Tests.dll`
   (121 tests, 102 skipped). A rename must not change any of those numbers.
7. **Record it:** every rename goes in [refactoring-ledger.md](refactoring-ledger.md) (old name → new
   name, and why), as PROC-5 requires.
8. **Do not edit** the dated records (`arcade-fidelity-notes.md`, the ledgers, `status.md`, `plan.md`,
   the handoffs, and this file) — the author keeps those.
9. **Line numbers are from the working tree on 2026-10-11.** Find the code by the member name and the
   quoted text, not by the number.
10. **Suggested names are suggestions.** If the name after `*Fix:*` fights an existing word in
    [glossary.md](glossary.md), the glossary wins; use the glossary's word and say so in the commit.

### Suggested order

A (names that say the wrong thing), then C (one name, several types), then D (units), B (vague
names), E (booleans), F (abbreviations), G (tests). Section I is for the author.

### How much each item touches

"Sites" counts whole-word uses in the files the item names, in code only (comments and strings
excluded), so it is the size of the rename, not the size of the edit.

| Item | Kind | Declarations | Sites |
|---|---|---|---|
| N01 `now` | param + local | 12 + 4 | 59 (4 files) |
| N02 `keys` / `state` | param | 12 | 30 + 4 |
| N03 `manager` | param | 19 | 49 |
| N04 `context` | param | 12 | 59 (11 files) |
| N05 `texture` / `frame` | param | 7 | 20 + 7 |
| N06 `box` / `rect` / `sprite` / `source` | param | 10 | 16 + 8 |
| N13 `line` (int) | param + field + property | 23 | 82 (6 files) |
| N34 `a` / `b` | param | 14 | 27 + 12 (tests) |
| N40 `w` / `h` / `R` / `G` / `B` / `s` / `c` / `hi` | param | 11 | ~40 |

---

## A. Names that say the wrong thing

These mislead: a reader who trusts the name gets the type, the unit, or the meaning wrong.

**N01. The input poll is called `now`, which reads as a time.**
`InputSnapshot now` in `DefineInputsState` (`CaptureInput`, `WasKeyboardPressed`, `Navigate`,
`ScrollOrArm`, `WasPadPressed`), `DeveloperToolsState` (`WasKeyboardPressed`, `WasPressed`,
`WasPadPressed`), `SettingsState` (`WasKeyboardPressed`, `WasPressed`, `WasPadPressed`) and
`SoundTestState` (`IsPressed`) — 12 parameters — and the 4 locals that feed them
(`var now = InputSnapshot.Read();`).
*Fix:* `currentInput` on the parameters, and `var currentInput = InputSnapshot.Read();` for the
locals, so the pair reads `currentInput` / `_previousSnapshot` and matches the author's
`currentInputState` in `ButtonEdgeDetector.Advance`.
*Rule:* NAM-19 (a name that needs a comment to say what it holds is wrong).
*Also in this item:* `HighScorePaletteTests.cs:162` `var now = (byte)palette.GetSlotValue(9);` — a
palette slot's value, not a time → `slotValue`.
*Done when:* `now` appears in the solution only in prose.

**N02. `keys` and `state` both hold a `KeyboardState`, and neither says so.**
`keys` in `ActionBinding.IsHeld`, `InputBinding.IsHeld`, `ControlSettings.IsPauseHeld`,
`ControlSettings.ReadPlayer`, `PlayerControls.IsFiring`, `PlayerControls.GetMoveDirection`,
`PlayerControls.GetShootDirection`, `PlayerControls.GetAxis`, `ControlCaptureTests.Bare`;
`state` in `RobotronGame.HandleAttractDevKeys`, `HandlePresentationKeys`, `HandleStartKeys`,
`HandleDeveloperToolsKey`, `FakeInputSource`, `PlayerAnimationAndAimTests`,
`PlayFieldBrainProgMissileTests`, `DemoPlayerInputSourceTests`.
*Fix:* `keyboardState` in all of them — the same word in all places, as NAM-9 asks.
*Rule:* NAM-19 (`keys` reads as a collection of `Keys`; `state` says nothing).

**N03. `manager` is a role, not the thing.**
`GameStateManager manager` in `IGameState.Update` and every state's `Update`
(`AttractState`, `DefineInputsState`, `DeveloperToolsState`, `EntriesMaximumState`, `GameOverState`,
`HighScoreTableState`, `InitialsEntryState`, `PlayingState`, `SettingsState`, `SoundTestState`,
`StorylineState`, `TitleScreenState`, `WaveClearState`), plus `DefineInputsState.Navigate`,
`DeveloperToolsState.Choose`, `HighScoreTableState.Leave`, `PlayingState.HandlePlayerDeath`,
`PlayingState.HandleWaveCleared`.
*Fix:* `stateManager`, which is what the shell already calls it (`RobotronGame._stateManager`).
*Rule:* NAM-19 (a helper object is named for its class, not for its job).

**N04. `context` says nothing about what is in it.**
`WaveSpawnContext context` in `IWaveSpawner.Spawn` and all eleven spawners, plus
`FamilyWaveSpawner.SpawnKind`. The record's own members are named plainly (`Field`, `Entities`,
`Placement`, `Random`, `PlayerStart`, `FamilySlotsLeftOver`); the parameter that carries them is the
vague one.
*Fix:* `waveSpawnContext`.
*Rule:* NAM-19.

**N05. A `Texture2D` is called `texture` or `frame`.**
`texture` in `BlitterDraw.DrawSprite`, `DrawSpriteSolid`, `DrawSpriteSolidWithBackground`,
`DrawSpriteSolidPiece`, `SpriteMask.CreateFromTexture`, `TitleScreenState.DrawCentredLogo`;
`frame` in `RobotTransporter.CreateImage`.
The codebase already has the right word: `SpriteCollision.GetMask(Texture2D animationFrame)`.
*Fix:* `animationFrame` for a single drawing (the glossary's word; "frame" on its own means a ROM
frame), `sprite` where the whole set is meant. `SpriteMask.CreateFromTexture` keeps its name — it
says what it makes — but its parameter becomes `animationFrame`.
*Rule:* NAM-19 (a texture ends in `Sprite`, `AnimationFrame` or `AnimationFrames`), glossary
("animation frame", never "frame" alone).

**N06. A `Rectangle` is called `box`, `rect`, `sprite` or `source`.**
`box` in `Prog.FitsInside`, `FieldEntities.IsClearOfElectrodes`, `PlayField.HitsWall`,
`PlayField.IsClearOfElectrodes`, `SpawnPlacement.IsFarEnough`; `rect` in `TunnelEffect.DrawEdge`,
`HulkAnimationTests.IsFullyInside`, `PlayFieldMovementTests.IsFullyInside`; `sprite`
(`Rectangle?`) in `StripEffectTests.NewExplosion`; `source` in `BlitterDraw.DrawSpriteSolidPiece`.
*Fix:* `bounds` (`entityBounds`, `spriteBounds`, `sourceBounds`, …) — the codebase's own rule.
`sourceBounds` also gets its summary kept, which is where the "of what, in whose pixels" lives.
*Rule:* NAM-19 (a rectangle ends in `Bounds`), NAM-5 (`rect`).

**N07. `player` is a position, not a player.**
`IntVector2 player` in `CruiseMissile.RollDirection` and `GruntChaseStep.GetNextPosition`. The
`CruiseMissile` constructor in the same file already calls the same value `playerPosition`.
*Fix:* `playerPosition` in both.
*Rule:* NAM-19, NAM-9 (one word, one meaning, in every file).

**N08. `line` means two unrelated things.**
Everywhere `line` is the row on a page — an `int`. In `ControlSettingsStore.AssignSlot` it is an
`ActionBinding`.
*Fix:* `lineBinding` in `AssignSlot`; the `int` rows are N13.
*Rule:* NAM-19 (one name, one type, everywhere).

**N09. `item` is a `MovieObject`.**
`item` in `AttractObjectMachine.BeginMono`, `StorylineState.DrawLaser`, `StorylineState.DrawMonoBox`.
*Fix:* `movieObject` (the class's word), or `monoMan`/`laser` where the caller is always one thing.
*Rule:* NAM-19.

**N10. `list` is the high score entries.**
`List<HighScoreEntry> list` in `HighScoreTable.Beats`, `CountInitials`, `Fill`, `Insert`,
`FindLowestInitialsIndex`, and the local `source` in `HighScoreTable.PadToCapacity`.
*Fix:* `entries` (the plural of what it holds), `sourceEntries` for the local.
*Rule:* NAM-19 (a collection is a plural noun for what is in it).

**N11. `input` is a single sound sample.**
`float input` in `SoundBoardRenderer.RemoveSteadyLevel`, whose summary has to say "This sample,
before the filter"; the field `_previousInput` is the same value one sample older.
*Fix:* `sampleBeforeFilter`, and `_previousSampleBeforeFilter`.
*Rule:* NAM-19 (a name that needs a comment to say what it holds is wrong).

**N12. `to` is a preposition used as a name.**
`IntVector2 to` in `ScreenSize.ToColumnAndRowDistance`.
*Fix:* `otherPosition`, and the summary names both ends: the distance from this position to that one,
in columns and rows.
*Rule:* NAM-21 (a distance says what it is measured between).

**N13. `line` is a place in a list, so it ends in `Index`.**
`int line` in `DefineInputsModel` (`GetAction`, `IsSpacer`, `PlayerOf`, `IsCursorOn`, `NextLine`,
`GetPreviousLine`, `SetLine`, `MoveTo`), `SettingsModel` (`IsActionLine`, `IsArmed`, `Activate`,
`GetLabel`, `GetValue`, `GetNote`, `GetActionHint`), `DefineInputsState` (`GetLabel`, `GetBinding`,
`DrawLine`, `DrawValue`), `DeveloperToolsState` (`Choose`, `Move`, `DrawOption`),
`SettingsState.DrawLine`, `SettingsModelTests.GoTo`; the field `_line` and the property `Line` in
`DeveloperToolsState`.
*Fix:* `lineIndex` on the parameters, `_lineIndex` / `LineIndex` on the field and property. The
method names keep `line` (a method names the thing it works on, not a place in it).
*Rule:* NAM-19 (a place in a list ends in `Index`).

---

## B. Names that say nothing

Not misleading, but the reader learns nothing from them.

**N14. `source` is used for five unrelated things.**
`MovieObject source` in `AttractObjectMachine.SpawnLaser` (the mono man that fires),
`IAnimationFrameSource source` in `StripEffect.CreateAppear` (the robot whose animation frame the
appear is cut from), `ISpriteSource source` in `SpriteSet`'s constructor and `BuildMiniMan`,
`ISoundSource source` in `SpeakerOutput.Play`, `SpeakerOutput.TopUp`, `WavFileWriter.Write`,
`Rectangle source` (N06).
*Fix:* `frameSource` for the animation-frame one (the word `SpriteCollision.GetShape` already uses),
`shooter` for the laser's owner, `spriteSource` / `soundSource` for the rest.
*Rule:* NAM-19, NAM-9.

**N15. `settings` is already the game's biggest word, and the sound ROM uses it for six bytes.**
`byte[] settings` in `SingSound.PlayOnceFrom`, `PlayEchoing`, `Move` and `CrowdRoarSound.LoadNoise`
(the ROM's `CY2`, `NAMP`, `DECAY`, `NFFLG`, `NFRQ2`); `Settings settings` in `FilteredNoise`'s
constructor and `Start`. Everywhere else `settings` is `GameSettings` (the GAME ADJUSTMENT page,
`settings.ini`) or `ControlSettings` (the DEFINE INPUTS page, `controls.ini`).
*Fix:* `settingBytes` for the `byte[]`, `soundSettings` for the `Settings`. The `Settings` **type**
in `Robotron2084.Audio.Synthesis` is a separate question — see I02.
*Rule:* NAM-9, NAM-19.

**N16. `data` is the bytes of a PNG chunk.**
`byte[] data` in `SpriteExtractor`'s `WritePng`, `ZlibCompress`, `WriteChunk`, `Crc32`.
*Fix:* `payload` (the bytes a chunk carries) or `bytes` where the code means any bytes.
*Rule:* NAM-19.

**N17. `parts` are the words of a control line.**
`string[] parts` in `InputBinding.TryParseGamePad` and `TryStick`; the summary already says "The
value's words".
*Fix:* `bindingWords`.
*Rule:* NAM-19 (the comment is carrying the name).

**N18. `number` is a message number, a sound number, and a player number.**
`int number` in `AttractPageMachine.GetMessageText`, `SoundBoard.ClearRepeatCounts`,
`DifficultyTuningTests.Wave`, and the local in `ControlSettingsStore.TryParse`.
*Fix:* `messageNumber`, `soundNumber`, `waveNumber`, `playerNumber`.
*Rule:* NAM-19.

**N19. `canvas` is a pattern of colours being drawn into, not the game's canvas.**
`Color[] canvas` in `SpriteFactory.FillRect`. The same file calls the same kind of array `pattern`
(`BuildLaserDiagonalMainPattern`, `ScalePattern`), and `canvas` elsewhere in the codebase means the
320×200 game canvas.
*Fix:* `pattern`.
*Rule:* NAM-9 (one word per concept).

**N20. `output` is a MonoGame stream, not the sound port.**
`DynamicSoundEffectInstance output` in `SpeakerOutput.TopUp` and `WaitForQueueToEmpty`.
(`_output` as a `BoardOutput` in the sound routines is fine: it is the board's output port, and the
name is the class's own word.)
*Fix:* `soundStream`.
*Rule:* NAM-19.

**N21. `current` is a letter, a register, and a snapshot.**
`char current` in `InitialsEntryModel.NextLetter` and `GetPreviousLetter` → `currentLetter`;
`byte current` and `byte written` in `Pia6821.KeepInterruptFlags` → `registerValue` and
`writtenBits`; `InputSnapshot current` / `InputSnapshot previous` in `ControlCapture.GetNewlyPressed`,
`GetNewlyPressedButton`, `GetNewlyPressedStick`, `KeyboardState current` / `previous` in
`ControlCapture.GetNewlyPressedKeys` and `RobotronGame.WasPressed` → `currentInput` / `previousInput`
and `currentKeys` / `previousKeys`.
*Rule:* NAM-19.

**N22. `scale` and `isPositive` leave the sum to the reader.**
`int scale` and `bool isPositive` in `Quark.ComputeAxisVelocitySubpixels`. `scale` is the axis's
multiplier (`QuarkTuning.VelocityXScale` / `VelocityYScale`) that turns the ROM's roll into
subpixels; `isPositive` decides the sign of the step, and the reader has to open the method to learn
what "positive" means on each axis.
*Fix:* `velocityScale`, and `isStepPositive` with the summary naming it ("positive is right or down").
*Rule:* NAM-19, NAM-21.

**N23. `top` and `bottom` are rows, and do not say so.**
`int top`, `int bottom` in `HighScoreTableState.DrawHatchedColumn` (with `int column`, which is
already the arcade's word).
*Fix:* `topRow`, `bottomRow`.
*Rule:* NAM-21 (a position names its origin), NAM-2.

**N24. `target` and `mine` are coordinates on whichever axis is being asked.**
`int target`, `int mine` in `CruiseMissile.AimSign`.
*Fix:* `targetCoordinate`, `ownCoordinate`, with the summary kept (it already says the axis moves).
*Rule:* NAM-19, NAM-21.

**N25. `isLarge` is a font, not a size.**
`bool isLarge` in `DefineInputsState.GetCenteredX`, `DeveloperToolsState.GetCenteredX`,
`SettingsState.GetCenteredX`, `TitleScreenState.GetCenteredX`, `HighScoreTableState.DrawRank`.
*Fix:* `isLargeFont`.
*Rule:* NAM-19.

---

## C. One name, several types (NAM-19, last bullet)

**N26. `input` is an `IPlayerInputSource`, a `PlayerInputState`, and a `float`.**
* `IPlayerInputSource input`: `GameServices.Input` (the record member),
  `GameServices.CreateFrom`, `GameSession.CreateNewGame`, `PlayField`'s constructor and its `Input`
  property, `PlayerSlot`, `GameOverState`'s constructor and `CreateFromSession`,
  `PlayerAnimationAndAimTests.CreateField`, `DemoPlayerInputSourceTests.EmptyFieldWithInput`,
  `PlayFieldBuilder.WithInput`.
* `PlayerInputState input`: `Player.UpdateFiring`, `HighScoreTableState.AnySwitchHeld`, and
  `InitialsEntryModel.Tick`, `IsHeld`, `CheckFireReleased`, `ReadSwitches`, `RepeatCycle`,
  `RepeatTypematic`, `Step`, `InitialsEntryModelTests.Tick`.
* `float input`: N11.
*Fix:* `inputSource` for the source (the class's word, as NAM-19 asks), `inputState` for the poll.
*Done when:* no `input` is left whose type is not obvious from the name.

**N27. `direction` is a `Direction8`, an `IntVector2`, an `int` sign, and a whole input state.**
* `int direction` (a −1/+1 sign): `InitialsEntryModel.IsHeld`, `BeginCycling`, `Cycle`,
  `SettingsModel.Change`, and the seven `GameSettings.Bump…` methods; the constants
  `InitialsEntryModel.UpDirection` / `DownDirection` are the same encoding.
* `PlayerInputState direction`: `InitialsEntryModelTests.Press`, `CommitLetter` (×2).
*Fix:* `directionSign` (or `stepSign`) for the −1/+1, `upStickSign` / `downStickSign` for the
constants, `inputState` for the test parameters. Where the type is `Direction8` the name is right and
stays.
*Rule:* NAM-19 (a direction held as −1 or +1 says so; `_direction` is a `Direction8`).

**N28. `state` is a `PlayerInputState`, a `KeyboardState`, and a `byte`.**
The `KeyboardState` ones are N02; `PlayerInputState state` in `FakeInputSource`,
`PlayerAnimationAndAimTests`, `DemoPlayerInputSourceTests` → `inputState`.
*Rule:* NAM-19.

---

## D. Numbers without their unit (NAM-2)

**N29. `size` and `Size` are port pixels.**
`int size` in `SpawnPlacement.FindSpawnPoint` and `PickRandomPointInside` (the field's own default,
`EntitySize`, says "in port pixels" in its comment); `static readonly int Size` in `PlayerLaser`,
`Spark` and `RescueScoreMarker` (all three are
`ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.…)`).
*Fix:* `sizePortPixels`, `SizePortPixels`.

**N30. `lifetime` is ROM frames.**
`int lifetime` in `AttractObjectMachine.SpawnLaser`; it is stored straight into
`MovieObject.LaserRomFramesLeft`.
*Fix:* `lifetimeRomFrames`.

**N31. `_ticks` counts the ticks of one playback.**
`PlaybackLength._ticks` (with `_limitTicks` and `_quietTicks` beside it, which do say what they count).
*Fix:* `_playedTicks`.

**N32. `N` is the side of a sprite, in arcade pixels.**
`const int N = 6` in `PlayerLaserAnimationFrameTests`.
*Fix:* `EdgeArcadePixels`.

**N33. `dx` / `dy` are a step, and an abbreviation.**
`int dx`, `int dy` in `AttractObjectMachine.ApplyStep`, `InputBinding.CreateStick`,
`GetDirectionCode`, `TryDirection` (×2), and the local pair in `Spark`; `IntVector2 delta` in
`Direction8Extensions.CreateFromDelta` and `Player.StepAxis`; `int delta` in
`DifficultyTuning.Adjust`.
*Fix:* `stepX` / `stepY` for a step, `deflectionX` / `deflectionY` for the stick's deflection,
`deltaFromTarget` (or `fromDelta`) where the record's name is `CreateFromDelta`; the `Adjust`
`delta` names its own thing in the summary and becomes `adjustmentSteps`.
*Rule:* NAM-5 (no abbreviations), NAM-21 (a delta names both ends).

**N34. `a` / `b` are not loop indices.**
`IntVector2 a`, `IntVector2 b` in `IntVector2.ComputeDistanceSquared` and `IsFartherThan`;
`SpriteShape a`, `SpriteShape b` in `IPixelCollision.Overlaps` and `SpriteCollision.Overlaps`;
`SpriteMask a`, `SpriteMask b` and `Rectangle a`, `Rectangle b` in `SpriteMask.Overlaps` and
`GetSharedScreenRect`; `IEntity a`, `IEntity b` in `BoxContactTest.Touches`,
`PixelContactTest.Touches`, `ICollisionScene.Touches`, `IContactTest.Touches`, `PlayField.Touches`;
the same in `PixelCollisionTests.Overlaps` and `PlayFieldBrainProgMissileTests.PixelDistance`.
*Fix:* `first` / `second` where the operation is symmetric (`Overlaps`, `Touches`,
`ComputeDistanceSquared`), `leftBounds` / `rightBounds` for `GetSharedScreenRect`.
*Rule:* NAM-6 (single letters are for loop indices).

---

## E. Booleans that are not predicates (NAM-4)

**N35. `wrapPass`, `frameFinished`, `Shrinks`.**
`bool wrapPass` in `Spheroid.AdvanceEscapeBeat` → `isWrapPass`; `bool frameFinished` in
`HighScorePrintSequence.Tick` → `isFrameFinished`; `Shrinks` (the `FilteredNoise.Settings` record's
own member, the ROM's `FDFLG`) → `LimitShrinks`, a predicate with its subject, as its neighbour
`IsDistorted` is.

**N36. `condition`, `carryOut`, `isHigh`, `quiet`, `solid`, `isFdb`.**
`bool condition` in `Mc6800Cpu.BranchIf` (the summary says "whether to take the branch") →
`takesBranch`; `bool carryOut` in `Mc6800Alu.Shifted` → `hasCarryOut`; `bool isHigh` in
`Pia6821.SetCb1` → `isCb1High` (the field already is); `bool quiet` in `SpriteExtractor.RunSprites`
and `ExtractOne` → `isQuiet`; `bool solid` in `WritePng` → `isSolid`; `out bool isFdb` in
`DataValues` → `isFdbData`.

**N37. `_reprogramMovingDown`, `_hasAppearBeenDrawn`, `_transportBegun`.**
`Brain._reprogramMovingDown` → `_reprogramStepIsDown`; `StripEffect._hasAppearBeenDrawn` →
`_appearIsDrawn` (the glossary's noun is *appear*, and "has appear been drawn" reads badly);
`WaveMaterialisation._transportBegun` → `_hasTransportBegun`.

**N38. `bug`, `invincible`, `attractShowing`, `soundOff` in the test builders.**
`PlayFieldBuilder.WithTankShellBug(bool bug)` / `.WithBrainsChaseMikeyBug(bool bug)` →
`tankShellBugEnabled` / `brainsChaseMikeyBugEnabled` (the fields beside them already use those
words); `PlayFieldBuilder.WithPlayerInvincible(bool invincible)` → `playerInvincibleForTesting`,
which is the name the production code uses; `GameSettingsTests.OnlyAnAttractScreenWithTheSoundSettingOffIsSilent(bool attractShowing,
bool soundOff, bool expected)` → `isAttractShowing`, `isAttractSoundOff`.

**N39. `_isAttractMode` is the odd one out.**
`WaveClearState._isAttractMode` / `isAttractMode` / `IsAttractMode` against
`GameSettings.IsAttractSilent(bool isAttractShowing)`, which is the switch it feeds.
*Fix:* `_isAttractShowing` / `isAttractShowing` / `IsAttractShowing`, so the mute switch reads
`IsAttractSilent(isAttractShowing)` at both ends.
*Rule:* NAM-9, NAM-7.

---

## F. Abbreviations and single letters (NAM-5)

**N40. `w`, `h`, `R`, `G`, `B`, `s`, `c`, `hi` in `SpriteExtractor` and `ArcadeText`.**
`int w`, `int h` in `TryParse` (×2) and `Ihdr` → `width`, `height`; `byte R`, `byte G`, `byte B` in
`Rgb` → `red`, `green`, `blue`; `Stream s` in `WriteChunk` → `stream`; `int hi` in `CollectData` →
`headerLineIndex` (it is the line the header sits on, and the summary writes it as `lines[hi]`);
`char c` in `ArcadeText.GetGlyphIndex` → `character`.
The `Mc6800Cpu` properties `A`, `X`, `SP` stay: they are the processor's own register names, quoted
as ROM labels.

---

## G. Test-only names (lowest priority, and the easiest to fix)

**N41. `Up`, `Down`, `Fire` are whole input states, not directions.**
`InitialsEntryModelTests`: `PlayerInputState Up`, `Down`, `Fire` (with `Idle`, which reads fine).
*Fix:* `StickUp`, `StickDown`, `FireHeld`.

**N42. `Sprite` and `Canvas` are rectangles; `Frame` is a `GameTime`; `State` is an input state.**
`StripEffectTests.Sprite` (`Rectangle`) → `SpriteBounds`; `PlayfieldLayoutTests.Canvas`
(`Rectangle`) → `CanvasBounds`; `GorfTests.Frame` (`GameTime`) → `GameTime` (the type's own word);
`PlayerAnimationAndAimTests.State`, `PlayFieldBrainProgMissileTests.State`,
`DemoPlayerInputSourceTests.State` (`PlayerInputState`) → `InputState`.
*Rule:* NAM-19, glossary ("frame" alone is a ROM frame).

**N43. `expected` on a `bool` is fine; `touching` on a `bool` is fine.** No item — recorded here so
the next pass does not raise them.

---

## H. Checked, and deliberately not items

* `Position` on every entity, and `position` on every parameter: an entity's own position, in the
  playfield's coordinates, which the interface's summary names. NAM-21's exemption.
* `bounds` / `_bounds`: the rule's own suffix.
* `manager`'s field, `_stateManager`: already the class's word.
* `_output` (a `BoardOutput`), `_sprites`, `_blitter`, `_highScoreStore`, `_frameAnimation`: the
  rule's own pattern.
* `Level` (a sound board's output level), `Kind`, `Mode`, `Count`, `Mask`, `Bounds`, `Value` (a
  digit, 0-9), `Number` (the ROM's `SND#`), `Code` (the ROM's direction code), `Target` (the robot a
  laser hit): each is a record member whose `<param>` names the thing, and each sits in a type whose
  name supplies the rest.
* `_beatTimer`, `_moveTimer`, `_clockUnits`, `_growClockUnitsRemaining`, `StepXPortPixels`,
  `SizeArcadePixels` and friends: the unit is in the name and the numbers match it.
* `A`, `X`, `SP` (`Mc6800Cpu`), `Or`/`Add`/`ExclusiveOr` (`Mc6800Opcodes`): the processor's own
  names, quoted as ROM labels (NAM-5's exemption). `Or` reads as the boolean operator to a reader who
  has not met the 6800; it stays, because the summary gives the mnemonic it stands for (`ORA/ORB`).
* `isOn` (`SettingsModel.GetOnOff`), `isArmed`, `isRightStick`, `isInset`, `writesBack`,
  `setsLoudness`, `isReachedByBranch`, `beamsIn`: predicates, and the words are the page's and the
  ROM's.
* `top` (`HighScoreTable`'s `TopScoreEntry`): the type and the property agree, which NAM-19 allows
  and wants.
* `now` in comments and prose: not an identifier.

## I. For the author to decide

**I01. `InputSnapshot` is the name of a poll, and four pages call it `now`.** N01 renames the
variables. If the type itself should be `InputPoll` (the glossary has "port tick", and
`InputSnapshot.Read()` reads like a file read), that is a vocabulary decision, not a rename.

**I02. `Settings` in `Robotron2084.Audio.Synthesis` is a second, unrelated `settings`.** The game's
`settings` are `GameSettings` (GAME ADJUSTMENT, `settings.ini`) and `ControlSettings` (DEFINE INPUTS,
`controls.ini`). The sound ROM's `Settings` is a six-number bundle for one sound routine. NAM-9 says
one word per concept; the fix is a type rename (`SoundSettingSet`? `RoutineSettings`?), which is the
author's call, and N15 is written so it does not pre-empt it.

**I03. `Human` / `HumanKind` still carry the retired word "human".** The glossary already records this: the word for the group
is *family member*, and the class `Human` and its `HumanKind` "still carry the old word until the
author decides on the rename". It is listed here because a pass over member names meets it
constantly (`Human`, `HumanKind`, the `player` in N07), and any decision about the word should be
taken once, for the type, its members and its tests together.

**I04. `MaxValue` (in `ArcadeRandom`) says the number, not the range.** `random.Next(MaxValue)` is
exclusive, so `MaxValue` is one more than the biggest value that comes out. `MaxValueExclusive` (or
`ValueCount`) says it without a comment. Left to the author, since the name is new.

---

## What is not in this list

Method names, class names and comments are covered by their own rules (NAM-1, NAM-13, NAM-18, CMT)
and by [code-review-issues.md](code-review-issues.md); this file is only about the names of values.
Nothing here changes what the game does: every item is a rename, and the build and the tests are the
proof.
