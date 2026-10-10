# Glossary

The words this project uses, in plain English. A `<summary>` in the code should read fine without this page.
When a summary has to use one of these words, it links here.

For where the original game's own names differ from ours, see [terminology-ledger.md](terminology-ledger.md).
For the rule that keeps this list to one word per idea, see [coding-standards.md](coding-standards.md) (CMT-6, CMT-11).

## Time

| Word | What it means | Name ending in code |
|---|---|---|
$1 The playfield calls every entity's `Update` on every tick, except during the freeze, and except for a robot that is still appearing at the start of a wave. Never write "nearly every tick". "Port" means this version of the game, as opposed to the original arcade machine. | `...Ticks` |
$1 In a comment, write "50ths of a second" or "50 times a second" instead, so a reader who does not know the arcade can follow it; keep "ROM frame" for names and this page.$2`...RomFrames` (never `...RomTicks`) |
| **clock unit** | A small counting unit that lets a port tick and a ROM frame both be whole numbers: a port tick is 5 units and a ROM frame is 6. A timer adds 5 every tick and goes off when it has 6 for each ROM frame it is waiting for. | `...ClockUnits`, or a `_...Timer` field whose summary says "clock units" |
| **beat** | One go of a character's own thinking and moving routine. It happens every few ROM frames. The character's own timer (a `_beatTimer` field) counts the ticks until it is due; the playfield does not count beats. | `...Beats` |
| **interval** | The gap from one beat to the next, or from one flicker, flash or other repeat to the next. A slower character has a longer interval. | `...IntervalRomFrames`, `...IntervalClockUnits`, `...IntervalBeats` |
$1| **freeze** | The short stop of the whole game, `HitStopTicks` ticks long, just after the player is killed. The playfield calls nobody's `Update` while it lasts. Not the same as the robots being frozen. | `...HitStop...` |
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
| **animation frame** | picture, art, image, frame (alone) | One drawing of a character, out of the set that makes it look like it is moving. A standing-still thing, like a shell, has just one. "Frame" on its own means a ROM frame. "Sprite" is fine too, as a name for the whole set of drawings a character has, or for one of them. |
| **palette slot** or **slot** | colour index, colour | One of the 16 colours the game can show at once. Slots 0-9 stay the same; 10-15 keep changing, which makes things flash. |
| **strip explosion** | explosion (on its own) | A robot's animation frame cut into thin strips that fly apart. |
| **appear** or **materialise** | spawn-in, warp-in | The strips of a new robot's animation frame rushing together at the start of a wave. |
| **transporter**, **transport** | warp-in, teleport | The arcade's own name for the way the robots arrive at the start of a brain wave: they are beamed in, sparkling, instead of forming out of strips. Any name in the code with `Transport` or `Transporter` in it is about this and nothing else. The word comes from the original source, `RRT2.ASM`, which is titled `TRANSPORTER`. |
| **death burst** | score burst | The flash and floating points left when a spheroid or quark dies. (Name still waiting for the author to decide.) |
| **electrode** | post | The spiky obstacle you must not touch. The original game calls it a "post"; this project calls it an electrode. |
| **reprogramming** | conversion, mutation | A brain turning a family member into a prog. |
| **family member** | human, civilian, humanoid | Mikey, Mommy and Daddy. The word for the group is **family member**, in names, comments and docs alike. Always call each one by the name the attract screen uses (never Mom, Dad, Mum or Mummy). The class `Human` and its `HumanKind` still carry the old word until the author decides on the rename (refactoring ledger). |
| **laser** (the player's shot); **spark**, **shell**, **cruise missile** (enemy shots) | bullet, projectile, missile (alone) | |
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
| "nearly every tick" | "on every tick, except ..." and say the exceptions | The playfield skips `Update` only in the freeze, and for robots still appearing. |
| ROM frame (in a comment) | "50ths of a second", "50 times a second" | A reader should not need the arcade's word to follow a comment. |
| picture | animation frame | The glossary's word for one drawing. |
| human (in prose) | family member | The class `Human` keeps its name until the author decides. |
| X and Y, axis (in a comment) | left or right, up or down, sideways | Plain words, not maths words. |

A comment is for a reader of about ten who has never seen the arcade game. If a word needs explaining, explain it in
place or leave it out.
