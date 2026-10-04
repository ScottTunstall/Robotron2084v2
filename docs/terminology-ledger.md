# Terminology ledger

This is a record of places where the **original Robotron 2084 source** (`ref/original-source/*.ASM`),
the author's own `asm/robomame.asm` disassembly, and this codebase use different words for the same
thing. Compiled while adding class-level XML docs to the entity classes in
`src/Robotron2084/Entities/`.

**Rule: the original source's own word wins.** Where the three columns disagree, new code, comments
and docs should reach for the "Original source term" column, not the disassembly's or an invented
one. This does not mean every class must be renamed today — it means the next time a name is
introduced or a comment is written, use the original's word.

| Concept | Original source term | `robomame.asm` term | This codebase | Notes |
|---|---|---|---|---|
| The basic walking robot | `ROBOT` (RRP8.ASM; sub-blocks `ROB0`..`ROB11`) | no dedicated label; comments just say "grunt" | `Grunt` | The codebase's name is a fan/community name, not the ROM's. |
| Hulk's bounds check | `CKLIMV` (RRH11.ASM) | inline wall-check logic, unnamed | (inline logic) | Same job, no shared name. |
| Tank's shot timer | `TNKSHT` (RRTK4.ASM) | `$BE64` "tank fire delay variable" | (inline field) | Different names, same concept. |
| Enforcer's re-aim | `ENFNV` (RRC11.ASM) | `PICK_ENFORCER_DESTINATION` | (inline logic) | Different names, same concept. |
| Turning a human into an enemy | **"mutate"** — `BMUT00` (RRB10.ASM) | "progging" (`brain_progging_flag`, `DRAW_BRAIN_IN_PROGGING_STATE`) | "reprogramming" (`Prog`) | The original's own verb is **mutate**, not "prog"/"progging". `coding-standards.md` already flags "reprogramming" as the chosen term (ROM `BMUT`) — this confirms the ROM label is `BMUT`/"mutate", and "prog"/"progging" is the disassembly's word, not the original's. |
| The recharging obstacle posts | **"post"** — `PSTKIL`, `PKPROC`, "*POST KILL PROCESS" (comments) | "electrode" | `Electrode` | The original never says "electrode" — it's a **post**. Both the disassembly and this codebase have standardised on the newer, non-original word. **Decided by the author: our names say "electrode", never "post" (coding-standards NAM-14).** |
| The brain's homing shot | Never called "cruise missile" — `BRNSHT` ("brain shot"), `CMISL`/`CMMOV` ("C-missile") (RRB10.ASM) | invented `CREATE_CRUISE_MISSILE`, comments say "cruise missile" | `CruiseMissile` | "Cruise missile" is a disassembler invention that stuck. Original's own words are **brain shot** / **C-missile**. |
| Tank's projectile | `SHELL`, `SHELLP`, `SHLDIE` (RRTK4.ASM) | "tank shell" | `TankShell` | Consistent across all three — no action needed. |
| Enforcer/spheroid's projectile | `SPARK`, fired by `ENFSHT` ("enforcer shot") | "spark (enforcer missile)" — the "(enforcer missile)" gloss is robomame's own addition | `Spark` | Core word "spark" is consistent; drop the "enforcer missile" gloss when writing about it, since the original doesn't use it. |
| The player's shot | `LASER` | "laser" | `PlayerLaser` | Consistent — no action needed. |
| Family members | `HUMAN`, `HUMATB` (RRH11.ASM) | "family member" throughout (`INITIALISE_FAMILY_MEMBERS`, `ANIMATE_FAMILY_MEMBER`, `FIND_NEAREST_FAMILY_MEMBER_TO_PROG`) | `Human` | The codebase's `Human` matches the **original**, not the disassembly. `coding-standards.md` already prefers "family"/"human" over "civilian"/"humanoid" — this confirms that's also the ROM's own word, and robomame's "family member" is the odd one out. |
| A reprogrammed human (enemy) | `PROG`, `PROGST`, "*START A PROG" (RRB10.ASM) | `PROG` (`CREATE_PROG`, `ANIMATE_PROG`) | `Prog` | All three agree — no action needed. (Contrast with "mutate"/"reprogramming" above, which is about the *act*, not the resulting enemy's name.) |
| The player character | `PLAYER` (a RAM jump-vector slot, RRF.ASM, invoked every interrupt from RRS22.ASM's `IRQV`) | `MOVE_PLAYER` | `Player` | Naming difference only, same routine/slot. |
| The rolling, splitting green enemy | `CIRCLE` (its death routines are `CIRKIL`/`CIRKP`) | `ANIMATE_SPHEROID` | `Spheroid` | The original calls it a **circle**, not a spheroid. "Spheroid" is the disassembly/fan name. |
| The enemy a spheroid splits into | `SQUARE` (its death routines are `SQKIL`/`CIRKV`) | `ANIMATE_QUARK` | `Quark` | The original calls it a **square**, not a quark. "Quark" is the disassembly/fan name. |
| The floating points shown on a spheroid/quark kill | `CIRKIL`/`CIRKP`/`SQKIL`/`CIRKV` (the same kill routines as above — no separate name for the number) | "drawing a spheroid's points value" (no dedicated label) | `ScoreBurst` | Neither source has a distinct name for this effect; "score burst" is this codebase's own term. |
| The bonus text/points for saving a family member | `PCFLG` (the rescue flag, in `HUMKIL`, RRH11.ASM) | `RESCUE_FAMILY_POINTS_TABLE` | `RescueScoreMarker` | Same concept, different names; neither source calls it a "marker". |
| The skull left where a family member died | Drawn in `HUMKIL`: picture `SKULP`, sound `HKSND` (RRH11.ASM) | no separately labelled routine | `SkullMarker` | No naming conflict, just no shared label to point at. |
| The fan-apart death effect | No single term — split across the death "explosion" routines (RRX7.ASM, RRHX4.ASM, RRDX2.ASM) and the separate shrink-in routine `APPEAR` (RRG23.ASM) | `MAKE_ENEMY_EXPLODE`, `CREATE_DIRECTIONAL_EXPLOSION` | `StripEffect` ("strip explosion" in prose) | `coding-standards.md` already names this **"strip explosion"** as the chosen codebase term "pending the author's decision" — worth noting neither the original source nor robomame.asm actually uses "strip" at all; both just say "explode"/"explosion". |
| An object's link to the next one in its list | The record's own `NEXT`/`NEXTZ` field (RRDX2.ASM) | "linked list of X" per kind (e.g. `spheroids_enforcers_quarks_sparks_shells`, `family_list_pointer`, `electrode_list_pointer`) | `EntityList` | This codebase's `EntityList` is a C#-only structure; neither source has an equivalent single name. |
| The player's limited number of shots on screen | `LCNT`, checked against 3 in `LSPROC` (RRG23.ASM), drawn by `LASER` (RRS22.ASM) | fire-cap check at `$31D5`-`$31E5`, unnamed | `LaserSlots` | Confirms the "three" limit; no shared name for the slot table itself. |
| One vertical-blank interval | **FRAME** (e.g. RRS22.ASM's `FRAME` counter, RRT2.ASM's "GLITTER FRAME"); the pause-and-jump macro is `NAP n,LABEL` (defined in RRF.ASM) | "ROM frame" (matches) | "ROM frame" (`...RomFrames`) | Already aligned — `coding-standards.md`'s "ROM frame" is the original's own "FRAME". |
| One call of the game's 1/60s update loop | No original term — the ROM has no fixed-timestep loop distinct from the vblank frame | n/a | "port tick" (`...Ticks`) | A pure engine concept with no ROM equivalent; nothing to reconcile. |
| One pass of an entity's own update routine, every N frames | No original term — the ROM just uses `NAP n,LABEL` to re-enter a routine after n frames, with no noun for "a pass" | n/a | "beat" (`...Beats`) | Also a pure invented term, same reasoning as "port tick": there's no ROM word to prefer here. |
| A sprite bitmap | **PICTURE** — RRP8.ASM: `*ON PICTURE OF OBJECT` | (uses "picture" too, e.g. sprite-position comments) | "animation frame"; "art" appears nowhere now (already renamed away per `coding-standards.md`'s stale-vocabulary scan) | The ROM's own word is **picture**, not "animation frame" — `coding-standards.md` already captures this ("picture — the ROM's word for a bitmap it blits"), so this codebase's remaining gap is that "animation frame" is still a modern coinage for "one picture in a sequence", not the original's term. |

| The byte sent to the sound board | **SND#** — "sound number" (`RRS22.ASM` table format above `SNDLDV`); the service-mode test calls the lines "SOUND LINE" (`RRTEST2.ASM`) | "sound to play" (comment at `$D3FC`) | `SoundEntry.SoundNumber` (was `Note`) | "Note" was wrong: the byte picks a whole effect on the sound board, not a pitch. Renamed to the original's word (notes §126). |
| Sound number `$13` | **"BACKY OFFY"** — background off (`RRS22.ASM` INIT writes raw `$2C`, which is sound number `$13`); its table is `TR1SND` "CLEAR THE SYSTEM" (`RRT2.ASM`) | (unnamed; sent from `$4607`) | `SoundTables.ClearTheSystem`; also ends `ShellRebound` | `$13` stops the looping background sounds. |
| Beaming the robots in at the start of a brain wave | **TRANSPORTER** (`RRT2.ASM`, `STTL TRANSPORTER`), started by `TRNSTV` "START TRANSPORTING"; `RRG23.ASM` starts it with `MAKP TRANST` | the overlay at `$4140`, which jumps to `$459B` "begin brain wave" | `RobotTransporter`, `TransportImage`, `TransporterTuning`, `WaveMaterialisation.DrawTransport` | The original's word is kept, on the author's decision. Summaries say "beamed in" to explain it, but names say transport. |
| The brain wave's beam-in hum | `TRSPRC`, "TRANSPORTER SOUND PROCESS" (`RRT2.ASM`), started by `TRANST`/`TRNSTV` | `PLAY_BRAIN_WAVE_WARP_IN_SOUNDS` (`$4607`); sound `$12` commented "warp in" | `TransporterSound` | robomame's "warp in" is a paraphrase; the original calls the effect the **transporter**. |
| The sound tables | Each `...SND` label and its comment, e.g. `LASSND` "LASER SOUND", `RBSND` "ROBOT HIT", `CRKSND` "CIRCLE KILL" | addresses only | `SoundTables.Laser`, `.RobotHit`, `.CircleKill`, … (notes §127) | The port's old names were guesses, and one was wrong: "PlayerLaser" was `ST2SND`, the two-player start. |
| Sending a sound past the priority check | `SDOUT` (the jump vector to `SNDOUT`, `RRF.ASM`) | "play sound in B" (`$D006` → `$D3B6`) | `SoundEngine.SendDirect` | |
| Asking for a sound | `SNDLD` → `SNDLDV`, "SOUND LOADER" (`RRS22.ASM`) | `PLAY_SOUND_PRIORITY` / "play sound with priority" (`$D3C7`) | `Sound.Play`, `SoundEngine.Play` | |
| Stepping the sound table each vblank | `SNDSEQ`, "SOUND SEQUENCER" (`RRS22.ASM`) | unnamed (`$D3E0`) | `SoundEngine.Tick` | |
| Handing a sound number to the board | `SNDOUT` (`RRS22.ASM`), writing `SOUND` (`$C80E`) | "tell sound board to get ready.. / to play sound" (`$D3B6`) | `SoundBoard.SendSoundNumber` | |
| The sequencer's variables | `SNDPRI` (priority), `SNDX` (table index), `SNDTMR` (timer, 16 ms units), `SNDREP` (repeat count) (`RRF.ASM`) | `$56`, `$54`, `$57`, `$58` | `SoundEngine.CurrentPriority`, `_entryIndex`, `_ticksLeftInSend`, `_repetitionsLeft`; a table line's `REPCNT`/`SNDTMR` are `SoundEntry.Repetitions`/`LengthVblanks` | |

## How to read this

- **Consistent already** (no action needed): tank shell, player's laser, prog.
- **Disassembly or codebase invented a new word** where the original had its own: spheroid (→ circle),
  quark (→ square), cruise missile (→ brain shot/C-missile), electrode (→ post), "progging" (→ mutate),
  "family member" in robomame.asm (→ human, which the codebase already gets right).
- **No original term exists at all** (fine to keep the codebase's own coinage, but don't claim it
  comes from the ROM): score burst, rescue score marker, skull marker, strip effect, entity list,
  laser slots.
