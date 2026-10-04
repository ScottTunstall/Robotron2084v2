# Glossary

The words this project uses, in plain English. A `<summary>` in the code should read fine without this page.
When a summary has to use one of these words, it links here.

For where the original game's own names differ from ours, see [terminology-ledger.md](terminology-ledger.md).
For the rule that keeps this list to one word per idea, see [coding-standards.md](coding-standards.md) (CMT-6, CMT-11).

## Time

| Word | What it means | Name ending in code |
|---|---|---|
| **port tick** | One go round the game loop: one call of `Update`. The game does 60 of these every second, and `PlayField.Update` hands each one to every entity. "Port" means this version of the game, as opposed to the original arcade machine. | `...Ticks` |
| **ROM frame** | One redraw of the original arcade screen. The arcade did 50 a second, so the original game counted its waiting times in these. One ROM frame is a bit longer than one port tick: 6 ROM frames last as long as 5 port ticks. | `...RomFrames` (never `...RomTicks`) |
| **clock unit** | A small counting unit that lets a port tick and a ROM frame both be whole numbers: a port tick is 5 units and a ROM frame is 6. A timer adds 5 every tick and goes off when it has 6 for each ROM frame it is waiting for. | `...ClockUnits`, or a `_...Timer` field whose summary says "clock units" |
| **beat** | One go of a character's own thinking and moving routine. It happens every few ROM frames. The character's own timer (a `_beatTimer` field) counts the ticks until it is due; the playfield does not count beats. | `...Beats` |
| **interval** | The gap from one beat to the next, or from one flicker, flash or other repeat to the next. A slower character has a longer interval. | `...IntervalRomFrames`, `...IntervalClockUnits`, `...IntervalBeats` |
| **wait** | The part of an interval the ROM sets by itself, such as the frames a brain sleeps after a beat. The interval is the wait plus the frame the beat takes. | `...WaitRomFrames` |

Never call the clock unit "fifths", "sixths" or "6ths".

## Space

| Word | What it means | Name ending in code |
|---|---|---|
| **arcade pixel** | One dot on the original arcade screen, which was 304 dots across and 256 down. | `...ArcadePixels` |
| **column** | The arcade's way of counting across: one column is 2 arcade pixels wide. | `...Columns` |
| **row** | The arcade's way of counting down: one row is 1 arcade pixel tall. | `...Rows` |
| **spec pixel** | One dot on the 320 by 200 layout in `spec.txt`. Used only to scale sizes up. | `...SpecPixels` |
| **port pixel** | One dot on the 640 by 400 screen this version of the game draws. Everything in the game is measured in these. | `...PortPixels` |
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
| **death burst** | score burst | The flash and floating points left when a spheroid or quark dies. (Name still waiting for the author to decide.) |
| **electrode** | post | The spiky obstacle you must not touch. The original game calls it a "post"; this project calls it an electrode. |
| **reprogramming** | conversion, mutation | A brain turning a family member into a prog. |
| **family** or **human** | civilian, humanoid | Mikey, Mommy and Daddy. Always call them by the names the attract screen uses (never Mom, Dad, Mum or Mummy). |
| **laser** (the player's shot); **spark**, **shell**, **cruise missile** (enemy shots) | bullet, projectile, missile (alone) | |
| **Shoot** | Aim, Fire (as a direction) | Using the second stick to pick which way to shoot. "Fire" means making a laser. |
| **Kill** | Deactivate, Destroy, Remove | Taking something off the field. |
| **INTRO2** | title screen, presentation page (alone) | The first page: the Williams logo, credits and the F-key menu. |
| **FAMPAG**, **HISTO**, **TABLE** | title page, story page, high score page | The arcade's own names for the family page, the story page and the high score table. |
