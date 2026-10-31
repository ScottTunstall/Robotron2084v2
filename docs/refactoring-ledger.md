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
6. **Why the author chose it**, in the author's own words, in the "Decisions and why" section of
   [coding-standards.md](coding-standards.md). If the author gave no reason, say so there.

---

## 2026-10-31: Human becomes FamilyMember

### What was wrong

`Human` is the class for Mikey, Mommy and Daddy, the family members the player rescues. The name says what kind of object it is, a
person, which is the wrong idea for a family member, and it clashes with the other meaning of "human" in the code: the person at the controls.

### What was changed

| Old name | New name |
|---|---|
| `Human` (class and `Entities/Human.cs`) | `FamilyMember`, `Entities/FamilyMember.cs` |
| `HumanKind`, `HumanKindExtensions` (class and files) | `FamilyMemberKind`, `FamilyMemberKindExtensions` |
| `HumanSpawnTests`, `PlayFieldHumanTests` (classes and files) | `FamilyMemberSpawnTests`, `PlayFieldFamilyMemberTests` |
| `HulkVsHumanCollisionRule`, `HulkKilledHumanResult`, `BrainCaughtHumanResult`, `PlayerRescuedHumanResult` (classes and files) | `HulkVsFamilyMemberCollisionRule`, `HulkKilledFamilyMemberResult`, `BrainCaughtFamilyMemberResult`, `PlayerRescuedFamilyMemberResult` |
| `HumanWave`, `HumanProgFinalConversion`, `RespondToBrainCatchingHuman` and other names with `Human` inside | the same with `FamilyMember` |
| variables and parameters `human`, `victimHuman`, `humanSpot`, `mikeyHuman`, `humans` and so on | `familyMember`, `victimFamilyMember`, `familyMemberSpot`, `familyMembers` |
| comments and summaries where "human" means the family member | "family member" |

### What was left alone, and why

- **`SoundTables.KillAHuman` and `SaveAHuman`**: the sound table names follow the ROM, which the author asked for earlier.
- **`MovieWalk.Human`**: a label for how the attract movie walks an object. The author chose to keep it.
- **`humanPlayerInputSource` and `humanInputState`**: they mean the person at the controls, not a family member.
- **`Humanoid`**: a different word.
- **Comments where "human" means a person** (the demo pausing like a person, a reader, the player at the coin door, and an author quote in a test).
- **`docs/arcade-fidelity-notes.md`**: it records the ROM's own analysis and keeps its wording.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors. Tests: the main suite passes with the same count as before (807 passed, 1 skipped)
and the sound suite passes (19 passed, 102 skipped). All text files are CRLF.

---

## 2026-10-31: SpriteFactory and SpriteContentPaths become TextureFactory and TexturePaths

### What was wrong

After `SpriteSet` became `TextureSet` and every `Texture2D` was named `…Texture`, these two classes still used "sprite" for what they hold:
`SpriteFactory` builds textures at run time, and `SpriteContentPaths` is the list of texture files in the content folder. The author named
the replacements.

### What was changed

| Old name | New name |
|---|---|
| `SpriteFactory` (class and file), `_spriteFactory`, `SpriteFactoryPatternTests` (class and file) | `TextureFactory`, `_textureFactory`, `TextureFactoryPatternTests` |
| `SpriteContentPaths` (class and file) | `TexturePaths` |
| `tools/generate-mgcb.py`, which writes that file, and a comment in `tools/extract-fonts.py` | write `TexturePaths.cs` with the class `TexturePaths` |

### What was left alone, and why

- **`Content/Sprites/` and the `"Sprites/..."` asset names**: they are the content folder and every asset's name; renaming them would touch
  `Content.mgcb` and 260 PNG paths. Not asked for.
- **`tools/SpriteExtractor`, `SpriteBatch`, `SpriteMask`, `SpriteShape`, `SpriteCollision`**: the extractor is a tool name, the rest are about
  sprites as drawn objects or are MonoGame's own name.
- **`generate-mgcb.py` was not run**: it writes LF line endings and a different layout, so running it would have rewritten `Content.mgcb`
  and the file in another style. Only its names were changed, so the next run writes the new name.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; tests unchanged.

---

## 2026-10-31: SpriteSet becomes TextureSet

### What was wrong

The author said `SpriteSet` was the wrong name because it "deals with Animation Frames". It holds about 45 textures: roughly 26 arrays of
animation frames, and about 20 others (the font glyphs, the laser bars, the skull, the title wordmark, the Williams logo, the cursor, the
spare-life icon, the wall pixel). `AnimationFrameSet` would have been wrong for those. After the textures were all renamed `…Texture(s)`,
`TextureSet` is the name that is true of everything in it.

### What was changed

| Old name | New name |
|---|---|
| `SpriteSet` (class and file), `spriteSet`, `_spriteSet`, `PlayField.SpriteSet` | `TextureSet`, `textureSet`, `_textureSet`, `PlayField.TextureSet` |
| `ISpriteSource` (interface and file), `spriteSource` | `ITextureSource`, `textureSource` |
| `ContentSpriteSource` (class and file) | `ContentTextureSource` |
| tests: `NoSpriteSource`, `TestSprites`, `SharedSpriteSet` | `NoTextureSource`, `TestTextures`, `SharedTextureSet` |

The comments that said "sprite set" now say "texture set".

### What was left alone, and why

- **`SpriteBatch`, `SpriteMask`, `SpriteShape`, `SpriteCollision`, `SpriteFactory`, `SpriteContentPaths`, `SpriteExtractor`**: they are about
  sprites as drawn objects, or are MonoGame's own name, not about this set. `SpriteFactory` and `SpriteContentPaths` were renamed in the entry above.
- **`BlitterDraw` and `ArcadeText` inside `TextureSet`**: they are drawing helpers, not textures (135 uses reach through the set for them).
  Taking them out is a design change, and was not asked for.
- **`docs/sprite-map.md` and the extractor's "definitive sprite source"**: that is the ROM listing, a different thing.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; tests unchanged.

---

## 2026-10-31: the spare-life icon is loaded from the ROM extract, not typed in

### What was wrong

`SpriteSet.BuildSpareLifeTexture` held the icon as 24 hand-typed bytes (the nibbles of the ROM's `MNPIC`) and built the texture at run time,
with its own `GetPaletteColour` to colour it. Every other sprite is extracted from the ROM into a PNG by `tools/SpriteExtractor`, so this was
the one place where art lived in code. The author asked why.

### How it got in

The extractor had already cut `MNPIC` (ROM `$3596`, 3 bytes by 8 rows) into `Sprites/PlayerExtra.png`, but under a name from an early guess
("player dead/carry pose? verify", notes line 353), and nothing loaded it. The notes (section 58.4) then decoded the same bytes by hand "so the
port can build it without a PNG", and the builder was written from that.

### What was changed

| Old | New |
|---|---|
| `SpriteSet.BuildSpareLifeTexture` (24 hand-typed bytes) and `SpriteSet.GetPaletteColour` | removed; `SpareLifeTexture = spriteSource.Load("Sprites/SpareLife")` |
| `Content/Sprites/PlayerExtra.png`, its `Content.mgcb` entry, its entry in `SpriteContentPaths`, and the extractor's `"MNPIC" => "PlayerExtra"` | `SpareLife.png`, `Sprites/SpareLife`, `"MNPIC" => "SpareLife"` (and `docs/sprite-map.md`) |

Before removing the builder the PNG was compared with it: the same 6 by 8 shape, and the four colours are identical (palette index 2 is
(240,64,0), the cycling marker for index 11 is (144,208,240), index 8 is (144,144,160), index 3 is (240,0,240)). The mgcb colour key is
(255,0,255), so nothing in the icon turns transparent.

### What was left alone, and why

- **`docs/arcade-fidelity-notes.md` section 58.4**, which describes building it at run time: it is a dated record and keeps what it said then.
- **`HudLayout.HudSpareLifeWidthPixels` and `HudSpareLifeHeightPixels`**: the HUD still uses them to place the icons.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; the content pipeline produced `SpareLife.xnb`; 808 and 121 tests, unchanged. No test
reads the icon's pixels (the tests use a stand-in sprite source), so the picture itself was checked by comparing the PNG with the old bytes, not
by running the game.

---

## 2026-10-31: anything of type Texture2D is named ...Texture (NAM-19)

### What was wrong

Textures were named four different ways: `...Sprite` (`_pointsSprite`, `EnforcerSprite`), `...AnimationFrame` and `...AnimationFrames`
(`animationFrame`, `GruntAnimationFrames`), `...Frames` (`BerzerkRobotIdleFrames`), and bare nouns (`glyph`, `FontLarge`). A reader
of `_pointsSprite` could not tell it was a `Texture2D`. The author's rule is that the type shows in the name, so every `Texture2D`
ends in `Texture` (and every array of them in `Textures`).

### What was changed

62 distinct names (80 declarations) in 40 files, plus three methods that return a `Texture2D`.

| Old name | New name |
|---|---|
| `…Sprite` (`EnforcerSprite`, `SkullSprite`, `LaserBarSprite`, `TankShellSprite`, `_pointsSprite`, `rimSprite`, `coreSprite`, `wallPixelSprite` ...) | `…Texture` (`EnforcerTexture`, `SkullTexture`, `LaserBarTexture`, `TankShellTexture`, `_pointsTexture`, `rimTexture` ...) |
| `…AnimationFrames` (`GruntAnimationFrames`, `BrainAnimationFrames`, `RescueBonusAnimationFrames` ...) | `…AnimationFrameTextures` |
| `BerzerkRobotWalkLeftFrames` and the other four `BerzerkRobot…Frames`, and a local `frames` | `BerzerkRobotWalkLeftAnimationFrameTextures` and so on, `animationFrameTextures`. A bare "frame" means an arcade frame, so a texture of a drawing says "animation frame" (the author's correction) |
| `animationFrame`, `_animationFrame` (a `Texture2D`) | `animationFrameTexture`, `_animationFrameTexture` |
| `glyph`, `glyphs`, `FontLarge`, `FontSmall`, `_fontLarge`, `fontLarge` | `glyphTexture`, `glyphTextures`, `FontLargeTextures`, `FontSmallTextures`, `_fontLargeTextures`, `fontLargeTextures` |
| `GetCurrentAnimationFrame()` (19 entities), `GetExplosionAnimationFrame()`, `BuildSpareLifeSprite()` | `GetCurrentAnimationFrameTexture()`, `GetExplosionAnimationFrameTexture()`, `BuildSpareLifeTexture()` |

### What was left alone, and why

- **`SpriteSet` and the other "sprite" types** (`SpriteFactory`, `SpriteMask`, `ISpriteSource`, `SpriteCollision`, `SpriteBatch`): they are
  types, not `Texture2D`s. The author says `SpriteSet` is the wrong name because it "deals with Animation Frames", but has not chosen a new
  one. It also holds the fonts, the laser bars, the HUD icons and the blitter, which are not animation frames. Options: `AnimationFrameSet`
  (the author's lean, but it leaves the fonts out), `TextureSet` (it matches the new rule) or `GraphicsSet`. Decided later the same day: `TextureSet` (see the entry above this one).
- **`Create…`, `CreateSolid` and `Load…`** on `ISpriteSource`: they are factory methods (NAM-13), not names of textures.
- **A lowercase `texture`** (a parameter in a handful of methods): it already ends in `texture`.
- **The glossary's words** "sprite" and "animation frame": they are still the words for the thing; only the C# names changed.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; 808 and 121 tests, unchanged.

---

## 2026-10-31: Visible when settable, IsVisible when read-only (NAM-27)

### What was wrong

NAM-27 said every visibility flag is `IsVisible`. The author pointed out that C# WinForms names a settable property `Visible` (and
`Enabled`), and keeps `Is…` for read-only questions.

### What was changed

| Old name | New name | Why |
|---|---|---|
| `MovieObject.IsBrainVisible { get; set; }` | `BrainVisible` | the attract machine sets it from outside the class |

### What was left alone, and why

- **`ScoreBurst.IsVisible { get; private set; }`**: only `ScoreBurst` sets it, so to every other class it is read-only.
- **`StripEffect.IsVisible()`, `PlayField.IsPlayerVisible()`, and the parameters `isBrainVisible`, `isSpareLivesVisible`, `isAttractVisible`**: methods and
  parameters are questions or values, not settable properties.
- **The other public settable bools that start with `Is`** are not visibility flags, so this rule does not cover them: `MovieObject.IsLaser`,
  `IsDead`, `IsOnList`, `IsMonoActive`, `AttractObjectMachine.IsShakingUp`, `IsAlive`, `BoardMemory.IsBackground1On`, `IsLaserBallBonusRepeating`,
  `IsOrganTuneNext`, and `Mc6800Cpu.IsInterruptRequested`. The same WinForms convention would rename them. The author decided to leave them alone (see NAM-4).

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; tests unchanged.

---

## 2026-10-31: IsVisible for every visibility flag (NAM-27)

### What was wrong

`ScoreBurst.ShowingPoints` was a bool that says whether something is visible, and the author said it should be `IsVisible` and follow the
standards. The same idea had five more names in the code, so the rule was written once (NAM-27) and applied to all of them.

### What was changed

| Old name | New name |
|---|---|
| `ScoreBurst.ShowingPoints` | `IsVisible` |
| `StripEffect.IsDrawn()` | `IsVisible()` |
| `PlayField.IsPlayerDrawn()` | `IsPlayerVisible()` |
| `MovieObject.ShowsMonoBrain`, `BeginMono(... showsBrain)` | `IsBrainVisible`, `isBrainVisible` |
| `ArcadeHud` parameter `showSpareLives` | `isSpareLivesVisible` |
| `GameSettings.IsAttractSilent(isAttractShowing)` and its test parameter | `isAttractVisible` |

### What was left alone, and why

- **`TitleScreenState._isShowingArcadeText`**: it chooses between two sets of text, it does not say whether something is visible.
- **`HasPlayerAppeared`, `HasPlayerDiagonalAppearStarted`, `StripEffect._hasAppearBeenDrawn`**: they say something has happened.
- **The `Points…` names in `ScoreBurst`** (`PointsBounds`, `PointsPaletteIndex`, `PointsStepsRemaining`, `_pointsSprite`): the author asked whether
  they need "Points" at all. The class draws two things, the burst shape and the number, so a word is needed to tell the two apart
  (`BurstPaletteIndex` against `PointsPaletteIndex`). Waiting for the author to choose that word.
- **A wrinkle in `ScoreBurst.IsVisible`**: it is true only once the burst shape is over and the number is showing. The burst shape is also
  visible before that. The summary says so.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; tests unchanged.

---

## 2026-10-31: rescue bonus, not rescue score or points (audit D06)

### What was wrong

The points for saving a family member were called a rescue score (`RescueScoreMarker`, `RescueScores`, `ShowRescueScore`), a rescue bonus
(`ScoreValues.RescueBonus`, `AwardRescueBonus`) and "points". The author chose **rescue bonus**, because "Points may infer a 2D point on
screen".

### What was changed

| Old name | New name |
|---|---|
| `RescueScoreMarker` (class and file) | `RescueBonusMarker` |
| `FieldEntities.RescueScores` | `RescueBonusMarkers` |
| `SpriteSet.RescueScoreDisplays` (the five "1000".."5000" textures) | `RescueBonusAnimationFrames` |
| `PlayField.ShowRescueScore` | `ShowRescueBonus` |
| test `Rescue_LeavesScoreDisplay_AtTheRescueSpot_ThatExpires` | `Rescue_LeavesBonusMarker_AtTheRescueSpot_ThatExpires` |

### What was left alone, and why

- **`AwardScore`, `NoteScore`, `ScoreBoard`**: they are about the score in general, not the rescue amount.
- **The ROM label `RESCUE_FAMILY_POINTS_TABLE`**, quoted in a summary.
- **The other "points" names, which have the same problem and were not part of this decision:** the death burst's numbers
  (`ScoreBurst.PointsPaletteIndex`, `PointsBounds`, `PointsSteps`, `ShowingPoints`, `PointsOffsetXArcadePixels`, `_pointsSprite`) and the
  score amount in `ExtraLifeEveryPoints`, `GetExtraLifeEveryPoints`, `FactoryExtraLifeEveryPoints`. Asked of the author in the audit (D06).

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; tests unchanged.

---

## 2026-10-31: strip explosion and strip effect (audit D05, in part)

### What was wrong

The author asked that "strip explosion" be used rather than "strip effect" where they mean the same thing. They do not: `StripEffect` is
the class for two things, an explosion (the strips fly apart) and an appear (the strips close up), so "strip explosion" is only the first.
The prose already used the two words that way. One name had it backwards: `StripExplosionTuning` held the numbers for both kinds.

### What was changed

| Old name | New name |
|---|---|
| `StripExplosionTuning` (class and file) | `StripEffectTuning` |

The glossary now defines **strip effect** (both kinds) beside **strip explosion** (the strips flying apart).

### What was left alone, and why

- **`StripEffect`, `StripEffectKind`, `HasRoomForStripEffect`**: they cover the explosion and the appear, so "effect" is the right word.
- **The bare "explosion" names** (`IExplodable.GetExplosionAnimationFrame`, `SpawnExplosion`, `CreateExplosion`, `MovieExplosion`,
  `ExplosionStartSizer`): the glossary says a bare "explosion" should be "strip explosion". They are not renamed yet because the author
  has not said whether to; the audit lists them under D05.

### How it was checked

Debug build with 0 warnings and 0 errors; tests unchanged.

---

## 2026-10-31: British spelling, Developer, index, bounds and interval (audit R01, R03, R04, R05, R06 Part A)

### What was wrong

Five items from [terminology-duplicates-2026-10-31.md](terminology-duplicates-2026-10-31.md) where a standard already decided the word
(NAM-12, NAM-5, NAM-19, NAM-16), so no new decision was needed.

### What was changed

| Rule | Old name | New name |
|---|---|---|
| NAM-12 | `WallColorCycle` (class and file), `RobotronColor` (class and file), `ColorCycleEffect`, `ColorCycle`, `Content/Effects/ColorCycle.fx` (and its `Content.mgcb` entry and `Load` call) | `WallColourCycle`, `RobotronColour`, `ColourCycleEffect`, `ColourCycle`, `Content/Effects/ColourCycle.fx` |
| NAM-12 | `SolidColorParameter`, `UpdateEffectColors`, `PageColors`, `CurrentColor`, `ChaseColor`, `StepColor`, `ColorOperand`, `StartColorOperand`, `ColorOperandStep`, `ColorValues`, `FromColorValue`, `CreateFromColorValue`, `trailColor`, `flareColor`, `colorOverride`, `markerColors`, `fixedColor`, and every `color` parameter of our own | the same with `Colour` / `colour` |
| NAM-12 | `GetCenteredX`, `DrawCenteredLargeText`, `DrawCenteredSmallText`; "centered", "centers", "center" in our comments | `GetCentredX`, `DrawCentredLargeText`, `DrawCentredSmallText`; "centred", "centres", "centre" |
| NAM-12 | `Sound.Initialize` | `Sound.Initialise` |
| NAM-5 | `DevKeys` (class and file), `HandleAttractDevKeys` | `DeveloperKeys`, `HandleAttractDeveloperKeys` |
| NAM-19 | `_walkCycleStep` (`Brain`, `Hulk`, `Player`, `Prog`), `WalkCycleStep` (attract machine) | `_walkCycleIndex`, `WalkCycleIndex` |
| NAM-19 | `BlitterDraw.DrawnRect`, `SpriteFactory.FillRect`, `SpriteMask.GetSharedScreenRect` | `GetDrawnBounds`, `FillRectangle`, `GetSharedScreenEdges` |
| NAM-19 | a `Rectangle` called `box` or `rect`: `PlayField.HitsWall`, `IsClearOfElectrodes`, `Player`, `SpawnPlacement`, `TunnelEffect.DrawEdge`, `Prog.IsInside`, three test helpers | `bounds`, `steppedBounds`, `candidateBounds`, `edgeBounds`, `outerBounds` / `innerBounds`, `electrodeBounds`, `drawnBounds` |
| NAM-16 | `Grunt` and `BerzerkRobot`: `MoveDelayBeats`, `moveLimitBeats`, `DefaultMoveLimitBeats` | `MoveIntervalMaxBeats`, `moveIntervalMaxBeats`, `DefaultMoveIntervalMaxBeats` |
| NAM-16 | `Quark`: `_dropDelayBeats`, `dropDelayBeats`, `DefaultDropDelayBeats`; `Quark` and `Spheroid`: `RepeatDropDelayDivisor` | `_dropIntervalMaxBeats`, `dropIntervalMaxBeats`, `DefaultDropIntervalMaxBeats`; `RepeatDropIntervalDivisor` |

Two names are the fixer's own choice and are open to the author: `MoveIntervalMaxBeats` (the number is the most beats a robot waits,
picked at random each time, so it is an interval and a maximum), and `GetSharedScreenEdges` (the method returns the four edges of the
overlap as ints, not a rectangle).

### What was left alone, and why

- **The shader's own parameter names** `"RemapColor"` and `"SlotId"`: they are names inside the compiled shader, not C# names.
- **`DisplayInfo.Rect`**: it mirrors the Windows `RECT` structure for a native call.
- **The wave-table column names** (`GruntMoveDelay`, `SpheroidDropDelay` ...): audit item R06 Part B, which needs the author.
- **The `DropDelayRotations` of the spheroid**: audit item D13.
- **Comments that quote the ROM** (`COLOR $44`) and the `Color` type of MonoGame.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors; the content pipeline produced `ColourCycle.xnb`; 808 and 121 tests, unchanged.
The shader is loaded only when the game runs, so the renamed `Effects/ColourCycle` load has not been exercised by a test.

---

## 2026-10-31: every object is named with its whole type name (NAM-26)

### What was wrong

The author reviews code on paper and in diffs, with no IntelliSense, and could not tell the type of a parameter from its name:
"in WaveSpawnContext there is a parameter named \"Field\" - but what field?" (the reason is in "Decisions and why" in
[coding-standards.md](coding-standards.md)). `field` was also a bad name for a second reason: since C# 14 it is a keyword inside a
property accessor. NAM-25 had even listed `PlayField field` as a good name. Audit item R07 in
[terminology-duplicates-2026-10-31.md](terminology-duplicates-2026-10-31.md) counted about 270 of the commonest.

### What was changed

A scan of every parameter, local, field, property and record component whose type is a class, interface or record of ours (or
a MonoGame service) found about 875 whose name did not contain the type's name. After this change the scan finds none, apart
from the intended exceptions below. 258 files changed; each change is a rename.

| Old name | New name |
|---|---|
| `PlayField field`, `_field`, `WaveSpawnContext.Field` | `playField`, `_playField`, `PlayField` |
| `SpriteSet sprites`, `_sprites`, `.Sprites` | `spriteSet`, `_spriteSet`, `.SpriteSet` |
| `GamePalette palette`, `.Palette` | `gamePalette`, `.GamePalette` |
| `FieldEntities entities`, `.Entities` | `fieldEntities`, `.FieldEntities` |
| `WaveParameters parameters`, `.Parameters` (on `PlayField`) | `waveParameters`, `.WaveParameters` |
| `GameSession session`, `GameStateManager manager`, `GameServices services` | `gameSession`, `gameStateManager`, `gameServices` |
| `IPlayerInputSource input`, `.Input` | `playerInputSource`, `.PlayerInputSource` |
| `ICollisionScene scene`, `WaveSpawnContext context` | `collisionScene`, `waveSpawnContext` |
| `BoardOutput output`, `BoardMemory memory`, `Mc6800Cpu cpu`, `SoundEngine engine`, `ISoundBoard board` | `boardOutput`, `boardMemory`, `mc6800Cpu`, `soundEngine`, `soundBoard` |
| `ArcadeFrameTimer _beatTimer`, `_moveTimer`, `_lifeTimer` and the rest | `_beatArcadeFrameTimer`, `_moveArcadeFrameTimer`, `_lifeArcadeFrameTimer` |
| `GamePadState padOne`, `padTwo` | `playerOneGamePadState`, `playerTwoGamePadState` |
| `IPlayerInputSource playerOne`, `_humanInput` | `firstPlayerInputSource`, `_humanPlayerInputSource` |
| `Human victim`, `Target` (on `Brain`) | `victimHuman`, `TargetHuman` |
| `IEntity` in `Touches(a, b)`, `LaserHitResult.Target` | `firstEntity` / `secondEntity`, `TargetEntity` |
| `entity`, `hit` and `slot` parameters on the `FieldEntities.Add` and `CollisionResponder` overloads | the lower-camel type name (`brain`, `gruntHitElectrodeResult`, `playerSlot`) |
| `Texture2D wallPixel`, `frame` | `wallPixelSprite`, `animationFrame` |
| test locals such as `var field`, `settings`, `parameters`, `sink`, `model`, `board`, `mikey` | `playField`, `gameSettings` or `controlSettings`, `waveParameters`, `recordingSink`, `defineInputsModel`, `soundBoard` or `scoreBoard`, `mikeyHuman` |
| test fixtures `Tick`, `Tick16`, `OneTick`, `Dot`, `NoPad`, `Shared`, `Blank` | `TickGameTime`, `Tick16GameTime`, `OneTickGameTime`, `DotSpriteMask`, `NoGamePadState`, `SharedSpriteSet`, `BlankHighScoreEntry` |
| `HighScoreTable.Top`, `FactoryTop` | `TopScoreEntry`, `FactoryTopScoreEntry` (the saved JSON key is still `"top"`) |

A name that was a role plus a thing keeps the role: `mikey` is `mikeyHuman`, `nearSide` is `nearSideTankShell`, `_beatTimer` is
`_beatArcadeFrameTimer`.

### What was left alone, and why

- **The `SoundTables` fields** (`BrainKill`, `CircleKill` ...): they are the ROM labels' own names, by the author's decision.
- **Collections** (`IReadOnlyList<Grunt> grunts`): named for what they hold; the rule is about objects.
- **Names another rule fixes:** `...Bounds`, `...Position`, `...Count`, `...Index`, unit suffixes, predicates, `random`, `spriteBatch`.
- **The test helpers `OneTick()` and `Tick()`**, which are methods that return a `GameTime`, not objects held in a name. NAM-13 is
  the rule for method names.
- **The wall's colour cycle** is `wallColorCycle` for now: its class is `WallColorCycle`, and the American spelling goes with R01.
- **A few pattern or generic uses a text tool cannot type** (a lambda parameter with no declared type, a name reused for two types in
  one file). The scan in coding-standards.md is the check, and the author's review is the backstop.

### How it was checked

Debug and Release builds with 0 warnings and 0 errors, then `Robotron2084.Tests` (808, 1 skipped) and
`RobotronSoundPlayer.Tests` (121, 102 skipped), unchanged. The scans in the coding standards print nothing. MonoGame's own
`Effect.Parameters` and an enumerator's `.Current` were caught by the compiler and put back.

---

## 2026-10-31: no "roll" for a random number

### What was wrong

The author does not like "roll" for random numbers. NAM-24 already retired it, but it was still in names and comments
(audit item R02 in [terminology-duplicates-2026-10-31.md](terminology-duplicates-2026-10-31.md)). NAM-24 now says it is the
author's preference and applies everywhere the word could stand for a random number.

### What was changed

| Old name | New name |
|---|---|
| `HulkWaveSpawner.SearchFamilyListRollAtMost` | `SearchFamilyListThreshold` |
| `HulkWaveSpawner.TargetRollSides` | `TargetMaxExclusive` |
| `AttractObjectMachine.RollUpTo` | `GetRandomBelow` |
| `SpawnPlacement.PercentSides` | `PercentMaxExclusive` |

Comments that said "rolls a number", "direction roll" or "every roll" now say "picks a random number", "random direction" or "every
random number" (the quark and spheroid spawners, `WaveParameters`, `WaveTable`, `WaveTableRow`, `SpawnPlacement`, `ScriptedRandom`,
`PlayFieldBrainProgMissileTests`).

### What was left alone, and why

- "roll" in other senses: a tank rolling along, "scroll", "stroll", "hand-rolled", "corollary".
- `CoinFlipSides` and `AxisFlipSides` (`Quark`, `Prog`): they are dice talk too ("sides"), but they are not the word "roll". The author
  has not been asked about them.

---

## 2026-10-31: palette index, not slot, for a colour

### What was wrong

`Slot` meant five things (audit item D07 in [terminology-duplicates-2026-10-31.md](terminology-duplicates-2026-10-31.md)). The
sharpest clash was `PlayerTuning.LaserSlot`, a colour (palette entry 10), beside `LaserSlots`, the store of three lasers. The author
chose **palette index** for the colour meaning, in every name.

### What was changed

| Old name | New name |
|---|---|
| every `...Slot` constant of a palette entry: `LaserSlot`, `BackgroundSlot`, `ShapeSlot`, `HeadSlot`, `TrailSlot`, `BurstSlot`, `PointsSlot`, `TextSlot`, `HeadingSlot`, `InkSlot`, `PlayerDeathFadeSlot`, `HighScoreTodaySlot`, `TitleWallSlot` and the rest in `Tuning/` and the screens | the same name ending `...PaletteIndex` |
| `GetSlotColour`, `GetSlotValue`, `SetSlot`, `SuspendSlot`, `ResumeSlot`, `IsSlotSuspended` | `GetPaletteIndexColour`, `GetPaletteIndexValue`, `SetPaletteIndexValue`, `SuspendPaletteIndex`, `ResumePaletteIndex`, `IsPaletteIndexSuspended` |
| `DefaultSlotValues`, `_slotValues`, `_suspendedSlots` | `DefaultPaletteValues`, `_paletteValues`, `_suspendedPaletteIndices` |
| `FontSlots` (class and file), `FirstCyclingSlot`, `LastCyclingSlot`, `CyclingSlotMarkers`, `BlackSlots`, `OwnedSlots`, `PlayerDeathFlashSlots` | `FontPaletteIndices`, `FirstCyclingPaletteIndex`, `LastCyclingPaletteIndex`, `CyclingPaletteIndexMarkers`, `BlackPaletteIndices`, `OwnedPaletteIndices`, `PlayerDeathFlashPaletteIndices` |
| `GetWallSlot`, `GetLaserWallSlot`, `GetElectrodeSlot`, `GetTintSlot`, `GetDeathSolidSlot`, `GetFrameStrokeSlot`, `GetWordmarkColorSlot`, `GetWordmarkRimSlot` | the same with `PaletteIndex` (the wordmark one is also spelt `Colour`) |
| `HudScoreSlotCurrent`, `HudScoreSlotIdle` | `HudScorePaletteIndexCurrent`, `HudScorePaletteIndexIdle` |
| `GetColourSlots`, `GetSlot` (the Williams ring and the high score table) | `GetColourPaletteIndices`, `GetPaletteIndex` |
| the plain `slot` / `slots` / `Slot` in the palette, graphics, HUD, tuning, attract-movie and screen files, and `FirstSlot`, `LastSlot`, `SlotCount` in the two page palettes | `paletteIndex` / `paletteIndices` / `PaletteIndex`, `FirstPaletteIndex`, `LastPaletteIndex`, `PaletteIndexCount` |
| `FontSlotsTests` and test names that held "Slot" | `FontPaletteIndicesTests` and the same names with "PaletteIndex" |

Comments follow: "palette slot" and "slot" for a colour now read "palette index".

### What was left alone, and why

- `PlayerSlot` (a player's record), the family member's place in `FamilyList` (`FamilySlot`, `FirstSlot`, `GetMemberInSlot`), `LaserSlots`
  (the three places for lasers) and the key half and pad half of a controls line (`AssignSlot`, `isPadSlot`, "device slot"). They are
  other ideas that share the word, and the audit's D07 still lists them for a decision.
- The Williams ring's `EmptySlotColumn` and `EmptySlotRow`: a "slot" there is the place a W sits on the ring.
- The shader parameter strings `"SlotId"` and `"RemapColor"`: they are names inside the compiled shader, not C# names.
- Sound-buffer "slots" in `Audio` comments (one sample per slot).

### How it was checked

`dotnet build Robotron2084.slnx` in Debug and Release, then both test assemblies; counts unchanged from the last entry.

---

## 2026-10-31: wave, lives and Max: one word each

### What was wrong

[terminology-duplicates-2026-10-31.md](terminology-duplicates-2026-10-31.md) found three ideas with two or more words in
our own names (NAM-7, NAM-9). The author chose the word for each:

- **Wave, not level.** `LevelParameters`' own summary said "everything that makes one wave", and `LevelParameterGenerator`
  said "works out what each wave contains". The arcade and the screen say WAVE.
- **Lives, not men, ships or mini man.** `Lives`, `SpareMen`, `HasMen`, `shipsPerGame`, `ExtraManEvery` and `MiniManSprite`
  were one idea. `BozoMode` passed `StartingLives` as `shipsPerGame` and `GetSpareMen()` as `spareMen` in one call.
- **Max and Min, not Cap, Limit or Maximum.** `EnforcerCap`, `TankCap`, `ShellCountLimit` and `MaximumDifficulty` were all
  "the most allowed".

The fourth decision, that the sound tables keep the ROM's names, needed no rename: every field of `SoundTables` was
already named after its ROM label's comment (`BrainKill` is "BRAIN KILL", not a port word), so the ledger's claim that the
table mixed two vocabularies was wrong. The summary of `SoundTables` now says the names are the ROM's on purpose.

### How it got in

Not traced. The wave and level words come from two stages of the port (the table row `WaveParameters`, then the wider
`LevelParameters` built from it).

### What was changed

| Old name | New name |
|---|---|
| `WaveParameters` (one row of the arcade's wave table) | `WaveTableRow` |
| `LevelParameters` | `WaveParameters` |
| `LevelParameterGenerator`, `LevelParameterGeneratorTests` | `WaveParameterGenerator`, `WaveParameterGeneratorTests` |
| `LevelNumber`, `levelNumber` | `WaveNumber`, `waveNumber` |
| `LevelTableRow`, `_levelTable`, `levelTablePath`, `Content/LevelTable.csv` | `CustomWaveTableRow`, `_customWaveTable`, `customWaveTablePath`, `Content/CustomWaveTable.csv` (its header column is `Wave`) |
| `IsLevelCleared`, `SkipLevelKey`, `SkipLevelHeld`, `StartingLevelNumber` | `IsWaveCleared`, `SkipWaveKey`, `SkipWaveHeld`, `StartingWaveNumber` |
| `EnemySpeedBonusCapPerLevel` | `EnemySpeedBonusMaxPerWave` |
| `SpareMen`, `spareMen`, `GetSpareMen`, `DrawSpareMen`, `showSpareMen` | `SpareLives`, `spareLives`, `GetSpareLives`, `DrawSpareLives`, `showSpareLives` |
| `HasMen`, `AnyPlayerSlotHasMen`, `SwitchToPlayerWithMen` | `HasLives`, `AnyPlayerSlotHasLives`, `SwitchToPlayerWithLives` |
| `GetDisplayedMen`, `HudMaxMen`, `HudMenOriginColumnP1/P2`, `HudMenPitchPixels`, `menColumn` | `GetDisplayedSpareLives`, `HudMaxSpareLives`, `HudLivesOriginColumnP1/P2`, `HudLivesPitchPixels`, `livesColumn` |
| `DrawScoresAndMen`, `GetScoresAndMenRowY` | `DrawScoresAndLives`, `GetScoresAndLivesRowY` |
| `shipsPerGame` | `startingLives` |
| `ExtraMan…` (`ExtraManEvery`, `ExtraManEveryValues`, `GetExtraManEveryPoints`, `FactoryExtraMan…`, `ExtraManLine`, `BumpExtraManEvery`) | `ExtraLife…` |
| `MiniManSprite`, `BuildMiniMan`, `DrawMiniMan`, `HudMiniMan…Pixels` | `SpareLifeSprite`, `BuildSpareLifeSprite`, `DrawSpareLife`, `HudSpareLife…Pixels` |
| `EnforcerCap`, `TankCap`, `GlobalActiveSparkCap`, `ShellCountLimit` | `MaxEnforcers`, `MaxTanks`, `MaxActiveSparks`, `MaxShellCount` |
| `QuarkSpeedCap`, `QuarkSpeedCapHeader`, `DefaultSpeedCap`, `_speedCap`, `speedCap` | `QuarkMaxSpeed`, `QuarkMaxSpeedHeader`, `DefaultMaxSpeed`, `_maxSpeed`, `maxSpeed` |
| `AllTimeInitialsCap`, `AllTimeTopInitialsCap`, `EnforceAllTimeInitialsCap`, `initialsCap`, `atCap` | `MaxAllTimeInitials`, `MaxAllTimeTopInitials`, `EnforceMaxAllTimeInitials`, `maxInitials`, `isAtMaxInitials` |
| `MinimumGruntCap`, `MaximumDifficulty`, `MaximumTurnsPerPlayer`, `MinimumTurnsPerPlayer` | `MinGruntCount`, `MaxDifficulty`, `MaxTurnsPerPlayer`, `MinTurnsPerPlayer` |

Test names that held an old word went with them (`IsLevelCleared_…`, `SkipLevelHeld_…`, `ALevelOnlyHoldsSoManyGrunts_…`,
`Generate_WithLevelTable_…`, `Generate_LevelsAbove40_…`). Comments that said "level" for a wave, or "man", "men" or "ship"
for a life, were reworded. The glossary has the new words and the retired ones.

### What was left alone, and why

- **The namespace and folder `Robotron2084.Level`.** It holds `PlayField`, the spawners and the collision rules, which are
  not only about waves. The author has not asked for it to change.
- **The arcade's on-screen text and the saved keys:** `EXTRA MAN EVERY`, `NO EXTRA MEN`, `TURNS PER PLAYER`, and
  `extramanevery=` / `turnsperplayer=` in `settings.ini`. A renamed key would silently throw away a saved setting.
- **`TurnsPerPlayer` and `TurnsLine`**, the arcade's name for the setting.
- **Sound "level" and sound "wave"** (`OutputLevel`, `SquareWaveSound`, `WaveTableSound`), and "level with" meaning equal height
  (`isTargetBelowOrLevel`). They are other ideas that share a spelling.
- **`GruntSpeedFloor`** and the other `Floor` names, which are the arcade's table columns for a minimum that works the other
  way round (a floor on a delay), and **`rightLimit`, `bottomLimit`, `gruntLimit`, `LapInsetLimit`**, which are positions or
  belong to the beat-interval clash (R06 in the audit).
- **`EntriesMaximumState` and `ReachedEntriesMaximum`**, which are named after the arcade's "5 ENTRIES MAXIMUM" page.
- **Every `SoundTables` field**, for the reason above.

### How it was checked

`dotnet build Robotron2084.slnx` in Debug and Release: 0 warnings, 0 errors. `Robotron2084.Tests`: 808 tests, 1 skipped
(no ROM dump), as before. `RobotronSoundPlayer.Tests`: 121 tests, 102 skipped, as before. No string literal was changed.

---

## 2026-10-04: the strip effects' one store of ten, and the robot list flag

### What was wrong

These were found while making the start of a wave faithful (notes §143). They are wrong reads of the ROM that had
been built into names and constants, so they are recorded here as bad code as well as in the notes as behaviour.

- **`StripExplosionTuning.MaxConcurrent` (10)** said that every explosion and every appear shared one store of ten
  records. The arcade has three strip routines with a store each. The name hid which store it was (NAM-19), and
  its comment gave the number as a fact about all three (CMT-4).
- **`StripExplosionTuning.SizerStep`** was used for every appear, though only the diagonal routine shrinks an
  appear by that much. The other two shrink it by half as much.
- **`RobotKindInfo.IsOnRobotList`** was a flag where the arcade has an order: the appear loop meets the robots in
  the reverse of the order they are set up in.
- **`PlayField.QueueMaterialise`** was called by seven spawners, two of which (spheroids and quarks) should never
  have called it. The field now queues the robots itself, so the method had no caller left (DEAD-1).
- **`PlayingState`'s constructor** announced a turn every time it ran, which made the reason for the announcement
  a side effect of who made the state.

### How it got in

- The one store of ten came from reading `RRDX2.ASM` alone (notes §35.5) and applying it to all three routines.
  The horizontal routine's real count is not in the source at all: the source asks for 8 and the assembled ROM
  links 2, so it could only be found in the disassembly.
- The rest: not traced.

### What was changed

| Old name | New name | Where |
|---|---|---|
| `StripExplosionTuning.MaxConcurrent` | `HorizontalPoolSize`, `DiagonalPoolSize` and `GetPoolSize(StripEngine)` | `Tuning/` |
| (none) | `StripExplosionTuning.SlowAppearSizerStep`, `SmallestAppearSpacing`, `SmallestClosedAppearSpacing` | `Tuning/` |
| (none) | `StripEngine`, `StripEffect.GetEngine()`, `StripEffect.IsDrawn()`, `StripEffect.CreateFollowingAppear` | `Entities/` |
| `RobotKindInfo.IsOnRobotList` (a flag) | `RobotListSetUpOrder` (a number, or null), and `IsOnRobotList()` (a method, NAM-10) | `Level/` |
| `PlayField.QueueMaterialise` | removed; the field calls `WaveMaterialisation.Queue` itself | `Level/` |
| `PlayField.CountRobotsOnTheRobotList` | `GetRobotsInAppearOrder` (the list, whose length is the count) | `Level/` |
| `WaveMaterialisation._assembling`, `_pendingRobots`, `_sequenceNumber` | `_appearNumbers`, `_robotsByAppearNumber`, `_passes` | `Level/` |
| `WaveMaterialisation.Advance(explosions, clip)` | `Advance(entities, playfieldBounds)` | `Level/` |
| `Hulk.Update`'s first-aim block | `Hulk.AimForTheFirstTime` | `Entities/` |
| `Spheroid.Update`'s and `Quark.Update`'s mover blocks | `AdvanceMover` | `Entities/` |
| `new PlayingState(sprites, store, session)` announcing a turn | `isStartOfTurn` parameter | `States/` |

### What was left alone, and why

- **`StripExplosionTuning.SizerStep` keeps its name.** It is still the step of an explosion and of a diagonal
  appear; its comment now says so.
- **The column fan is still laid out a pixel apart**, where the arcade's is a column (two pixels) apart. Notes
  §71 decided it for explosions and it was not reopened (notes §143.9).

### How it was checked

Debug and Release build with no warnings. 837 tests pass, 32 more than before. Not checked on screen.

---

## 2026-10-04: GruntSpeedProgression checked against the ROM, and four standards fixes

### What was wrong

The author asked whether `GruntSpeedProgression` is correct. It was read line by line against `GEXEC` (`RRG23.ASM`) and
`ROBK0` (`RRP8.ASM`). The logic matches. The class itself broke four rules:

1. **Names (NAM-19):** `_updateTimer` counted ticks to the next check, and `_scoredSinceLastPass` said "pass" where every
   comment said "check" and was not a predicate.
2. **Numbers written in comments (CMT-4):** "fewer than thirty grunts" in two summaries, and a constant that holds it.
3. **Unnamed numbers (NUM-1):** `18 * 15` and `15 * 15`.
4. **Feature envy (STR-8):** `Update` counted the live grunts with its own LINQ line.

### How it got in

Not traced.

### What was changed

| Old | New |
|---|---|
| `_updateTimer` | `_ticksUntilNextCheck` |
| `_scoredSinceLastPass` | `_hasPlayerScoredSinceLastCheck` |
| `FirstUpdateRomFrames`, `UpdateIntervalRomFrames` | `FirstCheckRomFrames`, `CheckIntervalRomFrames` (NAM-16: an interval) |
| `18 * 15`, `15 * 15` | `FirstCheckPasses * PassRomFrames`, `CheckIntervalPasses * PassRomFrames`, each part a named constant quoting its ROM line |
| `Update(IEnumerable<Grunt>)` counting with LINQ | `Update(EntityList<Grunt>)` asking `GetLiveCount()` |
| the number thirty in two summaries | a `cref` to `GruntCountThatHoldsTheFloor` |

### What was left alone, and why

- **The first check, checked against the ROM (answered).** `GEXEC` loads 18 into `PD` (`LDA #$12`, disassembly `$2A85`) and the
  `DEC PD,U` at `$2ABF` runs on the very first pass, so the 18th pass is 17 sleeps, **255 ROM frames, after `GEXEC` starts**, not 270.
  `SLEEPV` and `DISP` in `RRS22.ASM` confirm that `NAP 15` runs again 15 dispatches later. The later checks, 225 apart, are right.
  The bigger gap is where the count starts. `GEXEC` is reached only after the wave is set up (`PLS0A`), the robots appear
  (`APPEAR`, at least about 45 frames), `PLS1` and its four naps (32 frames more): about 80 frames or more after the wave starts on
  an ordinary wave, and 182 on a brain wave (`NAP 150,PLS1` and then the same 32). The port counts its 270 frames from when
  the field is made, so against the ROM its first check is early by about 60 frames on an ordinary wave and about 165 on a brain
  wave. **Fixed the same day** (notes §141): the count now starts when the game goes live and is 277 ROM frames, which is the arcade's
  22 to reach `GEXEC` and 255 more.
- **Grunts made mid-wave** (`MidWaveSpawner`, Gorf's drops) start with the wave's starting limit, not the current eased
  one. The ROM has one global limit that every grunt shares and makes no grunts mid-wave, so this is a port-only choice.
  **Decided by the author: it stays. Gorf drops slow grunts, and nothing is to be changed.**

### How it was checked

Debug build with no warnings and 789 tests passing, the same as before. Behaviour is unchanged.

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
| `WaveMaterialisation` | `RetireConverged` (a robot whose appear effect had finished) | `FinishAssemblingRobots` |
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
  `_showingPoints`, `_reprogramMovingDown`.
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
  the 153 only return a field (`Bounds => _bounds`) and are allowed. `Brain.IsReprogramming` is the first one converted (now a method).
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
