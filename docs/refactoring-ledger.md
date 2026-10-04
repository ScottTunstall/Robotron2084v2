# Refactoring ledger

This is the record of code that was changed because it broke the coding standard, not because the game had
to behave differently. [coding-standards.md](coding-standards.md) rule **PROC-5** says every such change is
written down here, in the same set of commits.

It is here for two reasons:

- **To find a thing under its new name.** The notes, the handoffs and the commit messages are a record of
  what was true when they were written, so they are not rewritten after a rename. If one of them names
  something that no longer exists, look for it in the "Old name" column below.
- **To see what kinds of bad code get in, and how.** Each entry says what was wrong and, where the git
  history shows it, how it got there. That is what tells us which rule or scan was missing.

## How to add an entry

Add a new `##` section at the top of the entries, with the date and a short title. Say, in this order:

1. **What was wrong**: the rule it broke, and how it was found.
2. **How it got in**: the commit, if the history shows it. If you did not trace it, write "not traced".
3. **What was changed**: a table of old name to new name.
4. **What was left alone, and why.**
5. **How it was checked**: the build and the tests.

---

## 2026-10-04: Collection methods that did not say what they work on (NAM-20)

### What was wrong

The author, looking at `PlayField.CountRescue()`: *what are we counting the rescue of?* The rule they gave: a method
that works on a collection, on a class that is not itself a collection, must have the type of the items in its name.
They also pointed out that **human** and **family member** were used for the same thing, and chose **family member**.
Found by listing every method whose body loops over or searches a collection, then reading the candidates.

### How it got in

Not traced.

### What was changed

| Class | Old name | New name |
|---|---|---|
| `PlayField` | `CountRescue` | `CountRescuedFamilyMembers` |
| `PlayField` | `PlayMovementSounds` (shells and grunts) | `PlayShellAndGruntMovementSounds` |
| `WaveSurvivors` | `CountFamily` | `CountFamilyMembers` |
| `GameSession` | `AnyMenLeft` | `AnyPlayerSlotHasMen` |
| `WaveMaterialisation` | `RetireConverged` | `RetireConvergedRobots` |
| `AttractPageMachine` | `ClearText` | `ClearTextCells` |

The glossary entry for family members now says the word is **family member**.

Three renames made first were then undone, because the class name already says what the items are (see NAM-20):
`GruntSpeedProgression.SpeedUp` and `Update`, `RobotTransporter.Begin`, and `AttractObjectMachine.MoveObjects` keep their
original names.

### What was left alone, and why

- **The `Human` class, `HumanKind`, `HumanWave`, the `...HumanResult` records and the test names that use**
  **"human"** (about 400 uses). Renaming a class is a bigger step than a method name, and the terminology ledger
  records that `Human` is the original source's own word (`HUMAN`, `HUMATB`). It is the author's decision, and it
  is waiting for it.
- **`ICollisionRule.Detect`** and its eight implementations. Each one loops over a kind of entity (the brain rule
  over brains and family members, the laser rule over lasers and robots), but the method is one name on one interface,
  and the class name (`BrainCatchRule`) already says which. Giving each its own method name would break the
  interface, so it needs the author to say whether the rule should apply to interface methods.
- **Methods on collection classes** (`EntityList`, `FamilyList`, `LaserSlots`, `LaserWallFlares`, `HighScoreTable`,
  `FieldEntities`), which are exempt by the rule.
- **Methods that loop over their own sample buffers or pixel arrays** (`Play`, `Render`, `Spread`, the `Draw...`
  methods on the tunnel and logo), which are not collections of domain things.
- **The methods in the list that only loop as a detail** (`Draw`, `Update`, `Parse`), which are named for what they do.

### How it was checked

The build reports one error, in `BrainVictimReleaseRule.cs`, from a rename of `Brain.IsReprogramming` to a method that
the author has started and not finished; the files for that are not part of this entry and were left alone. Nothing
else in the solution reports a diagnostic. The tests were not run again after these renames for that reason.

---

## 2026-10-04: Names that did not say what they hold (NAM-19)

### What was wrong

The author's rule, given on this date: *"Variables and fields should have meaningful names and use the
principle of least surprise. A field named 'walkfacing' which is actually an animation sequence is useless.
I should be able to guess what type the variable or field holds from its name."* It was then widened to
method names and property names, and became rule **NAM-19**.

Every field and property in `src/Robotron2084` was checked against its type, and every local variable,
parameter and method name was scanned. The bad names were of five kinds:

1. **The name said one kind of thing and the member held another.** `_facing` held a `WalkSequence`,
   `PlayField.Score` was a `ScoreBoard`, `Hulk._target` was a function, `SpriteSet.Enforcer` was a texture.
2. **One name was used for several types.** `_settings` was a `ControlSettings` in two classes, a
   `GameSettings` in four and a `SquareWaveVector` in one. The one `HighScoreStore` had three names.
3. **A boolean read as a thing or a count.** `LasersFiredThisUpdate`, `_arcadeText`, `TankShellBug`, and
   local variables such as `brain`, `rows`, `key` and `pad`.
4. **A name was vague or shortened.** `_pc`, `_wait`, `_remaining`, `_timer`, `_packed`, `tx`, `w`, `h`.
5. **A method was named as a noun**, or a method that answers yes or no did not read as a question:
   `Digits()`, `Layout()`, `Colours()`, `Pressed()`, `Held()`.

### How it got in

- **`_facing`** was left behind when the `WalkFacing` enum became `WalkSequence` on this date. The type was
  renamed and the fields that hold it were not (NAM-8).
- **The others: not traced.** Most are as old as the classes they are in.

### What was changed

Private members are listed with their class. A local variable or a parameter is marked *(local)*.

#### Entities

| Class | Old name | New name |
|---|---|---|
| `BerzerkRobot`, `Brain` | `_facing` | `_walkSequence` |
| `BerzerkRobot` | `Facing` | `WalkSequence` |
| `BerzerkRobot` | `GetFacingTowards` | `GetWalkSequenceTowards` |
| `BerzerkRobot`, `Brain`, `Player`, `Prog` | `facing`, `nextFacing` *(local)* | `walkSequence`, `nextWalkSequence` |
| `BerzerkRobot` | `player` *(local, an `IntVector2`)* | `playerPosition` |
| `Brain` | `targetDx` *(local)* | `targetDeltaX` |
| `Player` | `_animationSequence` | `_walkSequence` |
| `Player` | `_animationSequenceIndex` | `_walkCycleStep` |
| `Player` | `LasersFiredThisUpdate` | `FiredLaserThisUpdate` |
| `Player` | `fire`, `aim` *(local)* | `isFireHeld`, `aimDirection` |
| `Hulk` | `_target` (a `Func<IntVector2>`), and its constructor parameter `target` | `_getTargetPosition`, `getTargetPosition` |
| `Hulk` | `_aimed`, `_horizontal` | `_hasAimed`, `_isMovingHorizontally` |
| `Hulk` | `tx`, `ty`, `stepArcadePx` *(local)* | `targetX`, `targetY`, `stepArcadePixels` |
| `StripEffect` | `_animationFrameOf` | `_getAnimationFrame` |
| `StripEffect` | `_frames`, `_sizer`, `_timer` | `_romFramesRemaining`, `_spacingAccumulator`, `_romFrameTimer` |
| `StripEffect` | `rows`, `dest` *(local)* | `fansByRows`, `destination` |
| `ScoreBurst` | `_points` (a texture), and its constructor parameter `points` | `_pointsSprite`, `pointsSprite` |
| `ScoreBurst` | `_remaining`, `_timer` | `_burstStepsRemaining`, `_stepTimer` |
| `Quark` | `_animationFrame` (an index) | `_animationFrameIndex` |
| `Quark` | `_droppingTanks`, `_fleeing` | `_isDroppingTanks`, `_isFleeing` |
| `Quark` | `top`, `low`, `high`, `positive` *(local)* | `startsAtTop`, `topExitY`, `bottomExitY`, `isPositive` |
| `Spheroid` | `_rotation` (an animation frame index) | `_animationFrameIndex` |
| `Spheroid` | `_escapeDirection` (-1 or +1) | `_escapeDirectionSignX` |
| `Spheroid` | `_dropping`, `_escaping` | `_isDropping`, `_isEscaping` |
| `Gorf` | `_playfield` (a rectangle), and its constructor parameter | `_playfieldBounds`, `playfieldBounds` |
| `Gorf` | `_direction` (-1 or +1), `_step` | `_directionSignX`, `_stepCount` |
| `Tank` | `_step` (a direction) | `_stepDirection` |
| `Tank` | `frames`, `birth` *(local)* | `animationFrameCount`, `growBounds` |
| `Enforcer` | `_growthRemaining` | `_growClockUnitsRemaining` |
| `Spark` | `_remainingLife` | `_lifeClockUnitsRemaining` |
| `Spark` | `subpixelsPerPortPxPerMove` *(local)* | `subpixelsPerPortPixelPerMove` |
| `CruiseMissile` | `aim`, `leaving` *(local)* | `aimedCoordinate`, `positionBeforeMove` |
| `PlayerLaser` | `before` *(local)* | `boundsBeforeMove` |

#### Level

| Class | Old name | New name |
|---|---|---|
| `PlayField` | `Score` (a `ScoreBoard`) | `ScoreBoard` |
| `PlayField` | `_gruntSpeed`, `_midWave` | `_gruntSpeedProgression`, `_midWaveSpawner` |
| `PlayField` | `_tankShellBug`, and its constructor parameter | `_tankShellBugEnabled`, `tankShellBugEnabled` |
| `LaserHitResult` | `Robot` (a `RobotKindInfo`) | `KindInfo` |
| `PlayerSlot` | `SavedWave` (a `LevelParameters`) | `SavedWaveParameters` |
| `GameSession` | `Controls`, `Settings` | `ControlSettings`, `GameSettings` |
| `GameSession` | `other` *(local)* | `otherIndex` |
| `PixelContactTest` | `_boxes`, `_pixels` | `_boxContactTest`, `_pixelCollision` |
| `WaveMaterialisation` | `_pending` | `_pendingRobots` |
| `WaveMaterialisation` | `beamIn`, `appear` *(local)* | `beamsIn`, `appearEffect` |
| `LevelParameterGenerator` | `_table` | `_levelTable` |
| `ScoreBoard` | `_step` | `_extraLifeEveryPoints` |
| `PlayfieldWall` | `_cycle` | `_colourCycle` |
| `RobotTransporter` | `images`, `image`, `numbers`, `sharing`, `shares` *(local)* | `imageNumbers`, `imageNumber`, `imageNumbers`, `robotsSharingImage`, `sharesImage` |
| `WaveSurvivors` | `wave` (a `LevelParameters`), `made` *(local)* | `parameters`, `spheroidsFromEnforcers` |
| `TransportImage` | `colours` *(local)* | `colourPair` |
| `DifficultyTuning` | `effective`, `drops`, `subtract` *(local)* | `effectiveDifficulty`, `maxDropsX2`, `isSubtracted` |
| `HulkWaveSpawner` | `target` *(local, a function)* | `getTargetPosition` |

#### AttractMode

| Class | Old name | New name |
|---|---|---|
| `AttractMovie` | `Objects`, `Page` | `ObjectMachine`, `PageMachine` |
| `AttractMovie`, `AttractPageMachine` | `Finished` | `IsFinished` |
| `AttractPageMachine` | `_objects`, `_pc`, `_wait` | `_objectMachine`, `_scriptIndex`, `_waitRomFrames` |
| `AttractPageMachine` | `_text`, `Text` | `_textCells`, `TextCells` |
| `AttractPageMachine` | `_script`; `AttractObjectMachine._scripts` | `_scriptBytes` in both |
| `AttractPageMachine` | `op` *(local)* | `opcode` |
| `MovieProcess` | `Wait`, `Loop`, `MonoLeft`, `CycleLeft` | `WaitRomFrames`, `LoopPassesLeft`, `MonoStepsLeft`, `CycleAdvancesLeft` |
| `MovieProcess` | `Alive`, `ReprogramShakePhase`, `ReprogramShakeLeft`, `WalkCycle` | `IsAlive`, `IsShakingUp`, `ReprogramShakesLeft`, `WalkCycleStep` |
| `MovieObject` | `X`, `Y`, `XVelocity`, `YVelocity` (256ths) | `XSubpixels`, `YSubpixels`, `XVelocitySubpixels`, `YVelocitySubpixels` |
| `MovieObject` | `OnList`, `Dead`, `MonoBrain`, `MonoActive` | `IsOnList`, `IsDead`, `ShowsMonoBrain`, `IsMonoActive` |
| `AttractObjectMachine` | `box`, `image`, `brain`, `label`, `script`, `right` *(local)* | `boxSlot`, `silhouetteSlot`, `showsBrain`, `labelIndex`, `scriptAddress`, `firesRight` |

#### Graphics

| Class | Old name | New name |
|---|---|---|
| `SpriteSet` | `AttractCruise`, `CursorArrow`, `Enforcer`, `LaserBar`, `LaserColumn`, `LaserDiagonalAnti`, `LaserDiagonalMain`, `MiniMan`, `ProgBurst`, `Skull`, `TankShell`, `Title2084`, `TitleWordmarkCore`, `TitleWordmarkRim`, `WallPixel`, `WilliamsLogo` (each one texture) | the same name with `Sprite` on the end: `EnforcerSprite`, `TankShellSprite`, ... |
| `SpriteSet` | `Text` (an `ArcadeText`) | `TextRenderer` |
| `ArcadeText` | `_miniMan` | `_miniManSprite` |
| `WordmarkAppear` | `_origin` (a rectangle), `_core`, `_rim`, `_pictureHeight` | `_bounds`, `_coreSprite`, `_rimSprite`, `_spriteHeight` |
| `WordmarkAppear` | `everywhere`, `columns`, `filled` *(local)* | `wholeScreenClip`, `columnHasPixel`, `isFilled` |
| `WilliamsLogoBorder` | `_logo` (a mask), `_drawn`, `_slots`, `_slot` | `_logoMask`, `_drawnLogoCount`, `_logoPositions`, `_logoIndex` |
| `WilliamsLogoBorder` | `_moving`, `_firstMove` | `_isMoving`, `_isFirstMove` |
| `WilliamsLogoBorder` | `end`, `run` *(local)* | `runEnd`, `runBounds` |
| `TunnelEffect` | `_left`, `_right`, `_top`, `_bottom` | `_leftColumn`, `_rightColumn`, `_topRow`, `_bottomRow` |
| `TunnelEffect` | `_packed`, `Packed` | `_packedColourPair`, `PackedColourPair` |
| `TunnelEffect` | `Finished`, `Erasing` | `IsFinished`, `IsErasing` |
| `TunnelEffect` | `inset`, `x0`, `x1`, `x2` *(local)* | `isInset`, `leftX`, `middleX`, `rightX` |
| `SpriteMask` | `_opaque` | `_opaquePixels` |
| `DefineInputsHighlight` | `_step`; `period` *(local)* | `_stepIndex`; `stepClockUnits` |
| `BlitterDraw`, `SpriteFactory` | `w`, `h`, `sx`, `sy` *(local)* | `width`, `height`, `sourceX`, `sourceY` |

#### Hud, Input, Persistence

| Class | Old name | New name |
|---|---|---|
| `InitialsEntryModel` | `_position`, `Position` (a letter index) | `_letterIndex`, `LetterIndex` |
| `InitialsEntryModel` | `_rubAllowed` | `_isRubAllowed` |
| `HighScorePageHold` | `_checks` | `_checksWithSwitchDown` |
| `HighScoreFrameAnimation` | `_strokes`, `_erasing` | `_strokesDrawn`, `_isErasing` |
| `SettingsModel` | `_armed` (one flag per line); `on` *(local)* | `_armedLines`; `isOn` |
| `ScoreFormatter` | `printed`, `lastTwo`, `suppressed` *(local)* | `hasPrinted`, `isInLastTwo`, `isSuppressed` |
| `HighScoreTableLayout` | `zero` *(local)* | `zeroBasedIndex` |
| `PauseToggle` | `held` *(parameter)* | `isPauseHeld` |
| `ActionBinding` | `Key`, `Pad` (each an `InputBinding`) | `KeyBinding`, `PadBinding` |
| `ButtonPresses` | `Fire`, `StartOnePlayer`, `StartTwoPlayers`, `Any` | `FirePressed`, `StartOnePlayerPressed`, `StartTwoPlayersPressed`, `AnyPressed` |
| `ButtonEdgeDetector` | `_previous` | `_previousInputState` |
| `BoundPlayerInputSource` | `_settings` | `_controlSettings` |
| `InputBinding`, `GamePadSticks`, `ControlCapture` | `rightStick` *(parameter)* | `isRightStick` |
| `InputBinding` | `pad` *(local, a number)* | `padIndex` |
| `ControlSettings`, `ControlCapture`, `PlayerControls` | `own`, `stick`, `second` *(local)* | `ownPad`, `stickBinding`, `isSecondPlayer` |
| `GameSettings` | `TankShellBug`, `BrainsChaseMikeyBug`, `AttractModeSound`, and their `Factory...` constants | the same name with `Enabled` on the end |
| `GameSettings` | `attractShowing` *(parameter)* | `isAttractShowing` |
| `SubmitResult` | `EntriesMaximum` | `ReachedEntriesMaximum` |
| `HighScoreTable` | `today`, `intoToday`, `intoAllTime`, `maximum`, `cap`, `at` *(local)* | `topEnteredToday`, `enteredToday`, `enteredAllTime`, `reachedMaximum`, `initialsCap`, `insertIndex` |
| `ControlSettingsStore`, `GameSettingsStore` | `settings`, `padSlot` *(local)* | `controlSettings` or `gameSettings`, `isPadSlot` |

The settings file on disk is not changed: its keys (`tankshellbug`, `attractsound`, ...) are written as text
and do not come from the property names.

#### Audio, Palette

| Class | Old name | New name |
|---|---|---|
| `SoundBoard` | `_sound` (an enumerator), `_waveTable` | `_outputChanges`, `_waveTableSound` |
| `SoundBoardAudioSink` | `_mono`, `_stereo`, `_output`; `pcm` *(local)* | `_monoSamples`, `_stereoSamples`, `_soundEffect`; `pcmBytes` |
| `SquareWaveSound` | `_settings` (a `SquareWaveVector`) | `_vector` |
| `SquareWaveSound`, `WaveTableSound` | `vector` *(parameter, a number)* | `vectorIndex` |
| `WaveTableSound` | `_wave` (the wave's bytes); `settings`, `carries` *(local)* | `_waveSamples`; `vector`, `hasCarry` |
| `Sound`, `TransporterRequest` (tool) | `_transporter` (a `TransporterSound`) | `_transporterSound` |
| `StereoPanner` | `_left`, `_right`, `_targetLeft`, `_targetRight`; `frame` *(local)* | `_leftVolume`, `_rightVolume`, `_targetLeftVolume`, `_targetRightVolume`; `leftSampleIndex` |
| `WallColorCycle` | `_palette` (a list of colours), `_index` | `_colours`, `_colourIndex` |
| `TunnelPalette` | `_pointer`, `Pointer` | `_windowStartIndex`, `WindowStartIndex` |
| `HighScorePalette.Process`, `PaletteAnimator.Process` | `Table`, `Index` | `ColourTable`, `ColourIndex` |
| `GamePalette` | `_slots`, `_suspended`, `DefaultSlots` | `_slotValues`, `_suspendedSlots`, `DefaultSlotValues` |

#### States and the game class

| Class | Old name | New name |
|---|---|---|
| `GameServices` | `HighScores`, `Controls`, `Settings` | `HighScoreStore`, `ControlSettings`, `GameSettings` |
| every state | `_highScores` or `_store` (the `HighScoreStore`), and the parameter `highScores` | `_highScoreStore`, `highScoreStore` |
| `HighScoreTableState`, `ScoreEntryCeremony` | `_table` | `_highScoreTable` |
| `PlayingState`, `SettingsState`, `TitleScreenState`, `HighScoreTableState` | `_settings` (a `GameSettings`) | `_gameSettings` |
| `DefineInputsState` | `_settings` (a `ControlSettings`), `_controlStore` | `_controlSettings`, `_controlSettingsStore` |
| `SettingsState` | `_store` (a `GameSettingsStore`) | `_gameSettingsStore` |
| `DefineInputsState`, `SettingsState` | `_previous` | `_previousSnapshot` |
| `DefineInputsState` | `key`, `pad`, `armed` *(local)* | `hasKeyBinding`, `hasPadBinding`, `isArmed` |
| `HighScoreTableState` | `_colour`, `_frame`, `_hold`, `_print` | `_highScorePalette`, `_frameAnimation`, `_pageHold`, `_printSequence` |
| `HighScoreTableState` | `px`, `py` *(local)* | `portX`, `portY` |
| `TitleScreenState` | `_colour`, `_border`, `_controls`, `_arcadeText` | `_pagePalette`, `_logoBorder`, `_controlSettings`, `_isShowingArcadeText` |
| `WaveClearState` | `_attract`, and its constructor parameter | `_isAttractMode`, `isAttractMode` |
| `ScoreEntryCeremony` | `_pending` | `_pendingScores` |
| `GameOverState` | `controls`, `settings` *(parameter)* | `controlSettings`, `gameSettings` |
| `AttractState`, `PlayingState` | `human` (an input state), `slot`, `dead` *(local)* | `humanInputState`, `playerSlot`, `deadPlayerSlot` |
| the four menu states | `large` *(parameter)* | `isLarge` |
| `RobotronGame` | `_playfield` (a render target), `_canvas` (a rectangle), `_fullScreen` | `_renderTarget`, `_canvasBounds`, `_isFullScreen` |
| `RobotronGame` | `desktop` *(local)* | `desktopSize` |
| `SpriteExtractor` (tool) | `Palette.Rgb` (an array) | `Palette.Colours` |

#### Methods

| Class | Old name | New name |
|---|---|---|
| `ScoreFormatter` | `Digits`, `Layout`, `DigitAt` | `GetDigits`, `LayOutGlyphs`, `GetDigitAt` |
| `StripEffect` | `Layout`, `FanForShot`, `SpritePlacement` | `LayOutStrips`, `GetFanForShot`, `GetSpritePlacement` |
| `GamePalette` | `Color`, `SlotValue` | `GetColour`, `GetSlotValue` |
| `TunnelEffect` | `Colours`, `PixelX` | `GetColourSlots`, `ToPixelX` |
| `SpriteMask` | `Overlap`, `SharedScreenRect` | `Overlaps`, `GetSharedScreenRect` |
| `SpriteSet` | `GlyphNames`, `NumberedNames` | `GetGlyphNames`, `GetNumberedNames` |
| `HighScoreTableLayout` | `AllTimePosition`, `FrameStroke`, `FrameStrokeSlot`, `FramePixelIsLit` | `GetAllTimePosition`, `GetFrameStroke`, `GetFrameStrokeSlot`, `IsFramePixelLit` |
| `HighScoreTable` | `InitialsOf`, `LowestInitialsIndex`, `Pad` | `GetInitialsOf`, `FindLowestInitialsIndex`, `PadToCapacity` |
| `InitialsEntryModel` | `Held`, `PreviousLetter` | `IsHeld`, `GetPreviousLetter` |
| `DefineInputsModel`, `ArcadeHud` | `PreviousLine`, `ScoresAndMenRowY` | `GetPreviousLine`, `GetScoresAndMenRowY` |
| `PlayerControls`, `ControlSettings` | `Firing`, `Axis`, `PauseHeld` | `IsFiring`, `GetAxis`, `IsPauseHeld` |
| `GameSettings`, `PresentationPagePalette` | `AttractIsSilent`, `StepDue` | `IsAttractSilent`, `IsStepDue` |
| `RobotronGame`, `SettingsState`, `DefineInputsState` | `Pressed`, `KeyboardPressed`, `PadPressed` | `WasPressed`, `WasKeyboardPressed`, `WasPadPressed` |
| `Direction8Extensions`, `IntVector2` | `Opposite`, `DistanceSquared` | `GetOpposite`, `ComputeDistanceSquared` |
| `Quark`, `Tank`, `SpawnPlacement` | `AxisVelocitySubpixels`, `RandomPointIn`, `RandomPointInside`, `RandomSpheroidCandidate` | `ComputeAxisVelocitySubpixels`, `PickRandomPointIn`, `PickRandomPointInside`, `PickRandomSpheroidCandidate` |
| `AttractObjectMachine`, `MovieAnimationFrames` | `RandomUpTo`, `Frames` | `RollUpTo`, `GetAnimationFrames` |
| `SoundBoard` | `LowWaveTable`, `HighWaveTable`, `JumpTable`, `SquareWave` (each makes a routine) | `CreateLowWaveTableRoutine`, `CreateHighWaveTableRoutine`, `CreateJumpTableRoutine`, `CreateSquareWaveRoutine` |
| `SpinnerSound`, `ControlSettingsStore` | `LowWaitForSend`, `Name` | `GetLowWaitForSend`, `GetName` |

### What was left alone, and why

- **`MaxDropsX2`** (`LevelParameters`, `Gorf`, `Spheroid`, `Quark`, `DifficultyTuning`). "X2" reads like a
  coordinate, but it means "twice the most drops" and it is used everywhere the wave table is. Renaming it is a
  question for the author.
- **"Slot" has four meanings**: a palette slot, a player slot, a place in the family list, and a place a laser
  can be in (`LaserSlots.Slots`). The fields were made distinct where two meanings met in one class. The word
  itself needs the author to pick one meaning for it in the glossary.
- **Methods that do something and return `bool`.** `Tick`, `Add`, `Insert`, `Assign`, `SwitchToPlayerWithMen`,
  `BeginWalk`, `CountDown`, `ShrinkLimit`, `LowerPitches`, `MovePitch`, `ChangeLowWait`, `TakeNextChange`,
  `FindPlayableRange`, `EnforceAllTimeInitialsCap` and the attract machine's `Read...Op` family. The `bool` means
  something different in each ("finished", "an extra man was won", "carry on reading"). Each needs a `Try...`
  name or a result type, which is a change of shape and not of name.
- **Predicates that already read as a question** without `Is`: `Qualifies`, `Beats`, `AppliesTo`, `FitsInside`,
  `TopCarriesInitials`, and the fields `_rampsStarted`, `_paletteStarted`, `_restartHandled`, `_transportBegun`,
  `_showingPoints`, `_scoredSinceLastPass`, `_reprogramMovingDown`.
- **Plain local names whose type is clear from the line they are on**: `next`, `source`, `output`, `scale`,
  `delta`, `centre`, `candidate`, `step`.
- **Fields that share a name across two classes but are each right for their class**: `_kind`, `_model`,
  `_letters`, `_explosions`, `_processes`, `_pixels`.
- **`Quark._speedCap`** has no unit in its name. Its unit was not traced through the ROM routine.
- **The tests' own local variables and test names** were not checked. The tests were changed only where a
  renamed member made them stop compiling.
- **The law of Demeter (STR-11).** The scan in the standard finds 153 calls made through another object's
  property. 101 of them are `sprites.Blitter.Something(...)` and `sprites.TextRenderer.Something(...)`. The fix
  is a choice between giving `SpriteSet` methods of its own and passing the blitter in, and `SpriteSet` is
  already the standard's example of a class with too many jobs (STR-1). That choice is the author's, and it is a
  change of structure, so it was not made with the renames.
- **Calculated properties (NAM-10, tightened the same day).** The rule no longer allows even a one-line calculation
  in a property, and the scan in the standard finds 153 expression-bodied properties, many of them plain
  calculations (`HasMen`, `ExtraManEveryPoints`, `IsFinished`, `Spacing`, `RobotsFrozen`). Each is a public or
  internal member that callers use, so turning them into methods is a separate sweep, not done here. Some of
  the 153 only return a field (`Bounds => _bounds`) and are allowed.
- **The notes, the handoffs and `code-review-issues.md`** still use the old names. They are a record of their
  day. This ledger is how to read them.

### How it was checked

`dotnet build Robotron2084.slnx -c Debug` and `-c Release` with no warnings, and `dotnet test`: 789 tests pass,
the same number as before. Members used from other files were renamed at the places the compiler reported, so
a word that is also used for something else was not touched. A search of `src`, `tests` and `tools` for each
old name that is not an ordinary word finds nothing.

---

## 2026-10-04: Field comments that had come away from their fields (CMT-8)

### What was wrong

In `Spheroid`, `Quark`, `Human`, `Hulk` and `TunnelEffect`, the comment that explained a field sat above a
different field, or sat with two others in a pile above one field. In `Spheroid`, "how many enforcers this
spheroid still owes", "rotations left until the next enforcer drop" and "Counts up to the next move" were all
above the animation frame index. It was found while checking the field names: the comments were the only thing
that said what the vaguer names meant, and they were wrong.

### How it got in

Commit `cbef83e` (2026-09-27), which used a tool to put the members of each class in order. Each of these
comments was written at the end of its field's line. The tool moved the fields, and left each comment on a line
of its own where the field had been.

### What was changed

Each comment was matched to its field from the version before that commit (`80f355d`), and written as a
`<summary>` above the field it describes. In `TunnelEffect`, `// $5A82` went back to `StartRightColumn` and the
`LDA #$EF` note to the packed colour pair.

### What was left alone, and why

Only these five classes were repaired. Other files touched by that commit were not searched for the same thing.
A comment that starts a line of its own, directly above a field it does not describe, is the thing to look for.

### How it was checked

By reading each field's uses against its new summary. The build and the tests are as above.
