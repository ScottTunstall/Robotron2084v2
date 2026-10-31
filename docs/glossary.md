# Glossary

The words this project uses, in plain English. A `<summary>` in the code should read fine without this page.
When a summary has to use one of these words, it links here.

For where the original game's own names differ from ours, see [terminology-ledger.md](terminology-ledger.md).
For the rule that keeps this list to one word per idea, see [coding-standards.md](coding-standards.md) (CMT-6, CMT-11).

## Time

| Word | What it means | Name ending in code |
|---|---|---|
| **port tick** | One go round the game loop: one call of `Update`. The game does 60 of these every second. "Port" means this version of the game, as opposed to the original arcade machine. The playfield calls every entity's `Update` on every tick, except during the freeze, and except for a robot that is still appearing at the start of a wave. | `...Ticks` |
| **arcade frame** | One redraw of the original arcade screen. The arcade did 50 a second, so the original game counted its waiting times in these. One arcade frame is 1/50 of a second. In a comment, write "fiftieths of a second" (or "50 times a second") instead, so a reader who does not know the arcade can follow it. Keep "arcade frame" for names and this page. | `...ArcadeFrames` (never `...ArcadeTicks`) |
| **clock unit** | A small counting unit that lets a port tick and an arcade frame both be whole numbers: a port tick is 5 units and an arcade frame is 6, so one unit is 1/300 of a second. Nothing runs 300 times a second; it is only a way of counting. `ArcadeFrameTimer` uses it inside, and entities never see it. | `...ClockUnits` |
| **beat** | A character's turn to move or act. It comes every few arcade frames. The character's `_beatArcadeFrameTimer` (an `ArcadeFrameTimer`) says when it is due, and then the character calls its `ActOnBeat`; the playfield does not count beats. | `...Beats` |
| **interval** | The gap from one beat to the next, or from one move, grow step, animation frame, flash or other repeat to the next. A slower character has a longer interval. | `...IntervalArcadeFrames`, `...IntervalBeats` |
| **wait** | The part of an interval the ROM sets by itself, such as the frames a brain sleeps after a beat. The interval is the wait plus the frame the beat takes. | `...WaitArcadeFrames` |
| **freeze** | The short stop of the whole game, `HitStopTicks` ticks long, just after the player is killed. The playfield calls nobody's `Update` while it lasts. Not the same as the robots being frozen. | `...HitStop...` |
| **robots frozen** | The robots stand still until the start of a wave is over, and while the player is dying (`PlayField.RobotsFrozen()`). The playfield still calls their `Update`, and each robot checks for itself. | `RobotsFrozen` |

Never call the clock unit "fifths", "sixths" or "6ths".

## Space

| Word | What it means | Name ending in code |
|---|---|---|
| **arcade pixel** | One dot on the original arcade screen, which was 304 dots across and 256 down. This game's own screen is `WidthInArcadePixels` by `HeightInArcadePixels` of them (320 by 200). Sizes and distances copied from the arcade are in these. | `...ArcadePixels` |
| **column** | The arcade's way of counting across: one column is one byte of the arcade's video memory across, which holds `ArcadePixelsPerByte` (2) arcade pixels. | `...Columns` |
| **row** | The arcade's way of counting down: one row is 1 arcade pixel tall. | `...Rows` |
| **port pixel** | One dot on the 640 by 400 screen this version of the game draws. Everything in the game is measured in these. `PortPixelsPerArcadePixel` (2) of them across make one arcade pixel, and `ScreenSize.ToPortPixelsFromArcadePixels` does the sum. | `...PortPixels` |
| **canvas pixel** | One dot on the real window, after that screen is stretched to fit it. Only used inside `Presentation`. | (none) |
| **subpixel** | A 256th of a pixel. The arcade kept positions this precisely. | `...Subpixels` |

A table's layout column ("5 per column") must be named something else, such as `ListColumn`.

## Game things

| Say | Don't say | What it means |
|---|---|---|
| **animation frame** | picture, art, image, frame (alone) | One drawing of a character, out of the set that makes it look like it is moving. A standing-still thing, like a shell, has just one. "Frame" on its own means an arcade frame. "Sprite" is fine too, as a name for the whole set of drawings a character has, or for one of them. |
| **palette index** | slot, colour index, colour | One of the 16 colours the game can show at once, numbered 0 to 15. Indices 0-9 stay the same; 10-15 keep changing, which makes things flash. Names end in `...PaletteIndex` (`LaserPaletteIndex`, `GetPaletteIndexColour`) and several are `...PaletteIndices`. "Slot" is not used for this any more; it still names a place in a list: `PlayerSlot` (a player's record), a family member's place in `FamilyList`, and `LaserSlots` (the three places for lasers). |
| **strip explosion** | explosion (on its own) | A robot's animation frame cut into thin strips that fly apart. |
| **appear** or **materialise** | spawn-in, warp-in | The strips of a new robot's animation frame rushing together at the start of a wave. |
| **transporter**, **transport** | warp-in, teleport | The arcade's own name for the way the robots arrive at the start of a brain wave: they are beamed in, sparkling, instead of forming out of strips. Any name in the code with `Transport` or `Transporter` in it is about this and nothing else. The word comes from the original source, `RRT2.ASM`, which is titled `TRANSPORTER`. |
| **death burst** | score burst | The flash and floating points left when a spheroid or quark dies. (Name still waiting for the author to decide.) |
| **electrode** | post | The spiky obstacle you must not touch. The original game calls it a "post"; this project calls it an electrode. |
| **reprogramming** | conversion, mutation | A brain turning a family member into a prog. |
| **family member** | human, civilian, humanoid | Mikey, Mommy and Daddy. The word for the group is **family member**, in names, comments and docs alike. Always call each one by the name the attract screen uses (never Mom, Dad, Mum or Mummy). The class `Human` and its `HumanKind` still carry the old word until the author decides on the rename (refactoring ledger). |
| **laser** (the player's shot); **spark**, **shell**, **cruise missile** (enemy shots) | bullet, projectile, missile (alone) | |
| **wave** | level, stage, round | One round of the game: "WAVE 12" on the screen. The type that holds everything about one is `WaveParameters`; the arcade's own table row it starts from is `WaveTableRow`; a wave number is a `WaveNumber`. The folder and namespace `Level` still hold the playfield, the spawners and the collision rules. |
| **lives**, **spare lives**, **extra life** | men, man, ships, mini man | `Lives` counts every life a player has, including the one in play. **Spare lives** are the others (`GetSpareLives`, `HasLives`): the little icons next to the score. An **extra life** is the award for passing the score the EXTRA MAN EVERY setting names (`ExtraLifeEvery`). The arcade's own on-screen words stay as they are on the screen (`EXTRA MAN EVERY`, `TURNS PER PLAYER`), and so do the keys the game saves (`extramanevery=`). |
| **Max**, **Min** | cap, limit, maximum, minimum (in our own names) | The biggest and the smallest a number may be: `MaxEnforcers`, `MaxShellCount`, `QuarkMaxSpeed`, `MinGruntCount`, `MaxTurnsPerPlayer`. A name starts with `Max` or `Min`, or has it before the thing it limits, as the neighbouring names do. A ROM page or label that says "maximum" (`ENTRIES MAXIMUM`) keeps its own word. |
| **sound table names** | our own names for the characters | Every field of `SoundTables` is named after the ROM label's own comment, so the original's words stay there: `BrainKill` is "BRAIN KILL", `CircleKill` is "CIRCLE KILL" (a spheroid), `SquareKill` is "SQUARE KILL" (a quark), `PostKill` is "POST KILL" (an electrode), `KillAHuman` and `SaveAHuman` are "KILL A HUMAN" and "SAVE A HUMAN". Everywhere else the port's own names are used (see [terminology-ledger.md](terminology-ledger.md)). |
| **Shoot** | Aim, Fire (as a direction) | Using the second stick to pick which way to shoot. "Fire" means making a laser. |
| **Kill** | Deactivate, Destroy, Remove | Taking something off the field. |
| **INTRO2** | title screen, presentation page (alone) | The first page: the Williams logo, credits and the F-key menu. |
| **FAMPAG**, **HISTO**, **TABLE** | title page, story page, high score page | The arcade's own names for the family page, the story page and the high score table. |

## Retired words

Words that used to be used here and must not come back. Do not use them in a name, a comment or a doc. The dated records
(`status.md`, the ledgers, `arcade-fidelity-notes.md`) are left as they were written.

| Retired | Use instead | Why |
|---|---|---|
| spec pixel, `...SpecPixels`, "spec-px" | arcade pixel, `...ArcadePixels` | Nothing is driven by a spec. `spec.txt` is not in the repo. |
| `SpecScale` | `PortPixelsPerArcadePixel` | It is how many port pixels one arcade pixel is drawn as. |
| `SpecWidth`, `SpecHeight` | `WidthInArcadePixels`, `HeightInArcadePixels` | The size of the game's screen, in arcade pixels. |
| `ArcadePixelsPerColumn` | `ArcadePixelsPerByte` | A pixel takes 4 bits, so a byte holds two; a column is one byte across. |
| reaim, re-aim, `Reaim…`, and "aim" for a robot heading for a point (`AimZone…`, `AimOffsetFromTarget`) | new direction (`NewDirection…`) or new destination (`NewDestination…`); "heads for" | A robot does not aim, it heads for a spot. Only a shot is aimed (a spark, a tank shell, the player's lasers), so "aim" stays for those. |
| roll, re-roll, `Reroll…`, `…RollSides`, `…RollAtMost`, `RollUpTo` | random number, pick at random, pick again, `…MaxExclusive`, `…Threshold`, `GetRandomBelow` | Dice talk, and the author does not like the word for random numbers anywhere (NAM-24). The code draws a random number; a method that does is `GetRandom…` or `SetRandom…`; the cut-off it is compared with is a `…Threshold`. |
| "nearly every tick" | "on every tick, except ..." and say the exceptions | The playfield skips `Update` only in the freeze, and for robots still appearing. |
| arcade frame (in a comment) | "fiftieths of a second", "50 times a second" | A reader should not need the arcade's word to follow a comment. |
| picture | animation frame | The glossary's word for one drawing. |
| human (in prose) | family member | The class `Human` keeps its name until the author decides. |
| level (as a round of the game), `Level…`, `…PerLevel`, `LevelNumber`, `LevelParameters` | wave, `WaveNumber`, `WaveParameters` | The arcade says WAVE, and so does the screen. "Level" stays only for "level with" (equal height) and for a sound's level. |
| men, man, ships, mini man, `SpareMen`, `HasMen`, `ExtraMan…`, `shipsPerGame` | lives, spare lives, extra life, `SpareLives`, `HasLives`, `ExtraLife…`, `startingLives` | One word for what a player may lose. The arcade's on-screen text and the keys the game saves keep theirs. |
| cap, limit, maximum, minimum (in our own names): `EnforcerCap`, `ShellCountLimit`, `MaximumDifficulty` | `Max…` and `Min…`: `MaxEnforcers`, `MaxShellCount`, `MaxDifficulty` | One word for the biggest and one for the smallest. A position that is a limit on a line (`rightLimit`) is not a count and is left. |
| slot, `...Slot`, `GetSlotColour`, `FontSlots`, `DefaultSlotValues` (for a colour) | palette index, `...PaletteIndex`, `GetPaletteIndexColour`, `FontPaletteIndices`, `DefaultPaletteValues` | "Slot" meant five things, and the laser's colour (`LaserSlot`) sat beside the store of lasers (`LaserSlots`). Palette index is plain and says what the number is. |
| X and Y, axis (in a comment) | left or right, up or down, sideways | Plain words, not maths words. |

A comment is for a reader of about ten who has never seen the arcade game. If a word needs explaining, explain it in
place or leave it out.
