#!/usr/bin/env python3
"""Embed the arcade's ATTRACT MOVIE data (the story "HISTO" script and its
object scripts) into `src/Robotron2084/Level/Attract/AttractMovieData.cs`.

Why generated: docs/arcade-fidelity-notes.md §95.9 — the storyline is a byte
STREAM in the R5 ROM, and the port must run the ROM's own bytes rather than a
retyped transcription (retyping is how the old `PGXD` burst art rotted, §90).

What it emits (all verified against the ROM image; addresses are the R5 ones
the scripts cross-reference each other with):

  * `Histo`       — the page-script byte stream, $7FA3..$83B6 (the intro screen's
                    text crawl and every scene that follows it).
  * `Scripts`     — the object-script byte stream, $83B7..$878C, indexed by
                    `address - ScriptBase`.
  * `WalkHuman`   — HUMANA @ $03CF: the family's 4-direction walk table.
  * `WalkHulk`    — HLKANA @ $01CC: the hulk's.
  * `AnimTable`   — ANATAB @ $7DEF: the walkers' 4-frame picture cycle.
  * `FontWidths`  — the LARGE font's width-in-pixels byte per ROM character code
                    ($30..$5E), which owns the arcade's text pen advance
                    (BLIT_LARGE_CHARACTER: advance = width + 1 px, notes §96.2).

Usage:
    python tools/extract-attract-scripts.py      (rewrites the .cs file)
"""

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ROM = ROOT / "ref/rom/robotron64k.bin"
OUT = ROOT / "src/Robotron2084/Level/Attract/AttractMovieData.cs"
SPRITES = ROOT / "src/Robotron2084/Content/Sprites"

ROM_SIZE = 0x10000

HISTO_START, HISTO_END = 0x7FA3, 0x83B7
SCRIPT_START, SCRIPT_END = 0x83B7, 0x878D
WALK_HUMAN, WALK_LENGTH = 0x03CF, 52
WALK_HULK = 0x01CC
ANIM_TABLE = 0x7DEF
FONT_TABLE = 0xEC34
MESSAGE_TABLE = 0x6377  # FDB per message number, starting at 115 (MMOM)
MESSAGE_FIRST = 115
MESSAGE_COUNT = 12  # 115..126 (126 = NULMES, the empty "" that clears the row)

# The score-post pictures (the POSTS descriptor at $7E8F). Its table is at $3B05
# with 4-byte records, of which the ones the POSTER script actually selects —
# images 12 (the main post), 0, 4 and 8 (the three that fork off it) — are the
# named indices here. Each is a (width-in-bytes, height, data) record and the
# four data blobs tile the ROM contiguously, which is what confirms the reading.
POSTS_TABLE = 0x3B05
POST_IMAGES = (12, 0, 4, 8)


# The small font's non-ASCII character codes (RRSCRIPT.ASM: SPACE/EXPT/COMMA/
# PERIOD/COLON/HYPHEN/LPAREN/RPAREN).
MESSAGE_CHARS = {
    0x20: " ", 0x3A: " ", 0x3B: "!", 0x3C: ",", 0x3D: ".", 0x3F: ":", 0x40: "-",
    0x5B: "(", 0x5C: ")",
}


def message(rom: bytes, number: int) -> str:
    """One NUL-terminated message string, decoded to the port's characters."""
    pointer = (rom[MESSAGE_TABLE + (number - MESSAGE_FIRST) * 2] << 8) | rom[
        MESSAGE_TABLE + (number - MESSAGE_FIRST) * 2 + 1
    ]
    out = []
    while rom[pointer] != 0:
        code = rom[pointer]
        out.append(MESSAGE_CHARS.get(code, chr(code) if 0x30 <= code <= 0x5E else " "))
        pointer += 1
    return "".join(out)



def byte_array(name: str, rom: bytes, start: int, length: int, comment: str) -> str:
    rows = []
    for i in range(0, length, 12):
        # Exactly `length` bytes: a short table must not run on into the ROM bytes
        # that follow it (ANATAB is 4 bytes, and reading 12 made the walkers cycle
        # through CODE as picture numbers).
        chunk = rom[start + i:start + min(i + 12, length)]
        rows.append("        " + " ".join(f"0x{b:02X}," for b in chunk))
    body = "\n".join(rows)
    return (
        f"    /// <summary>{comment}</summary>\n"
        f"    public static readonly byte[] {name} =\n    [\n{body}\n    ];\n"
    )


def post_records(rom: bytes) -> dict[int, tuple[int, int, int]]:
    """The POSTS picture records the script names: image index -> (widthB, height, data)."""
    records = {}
    for image in POST_IMAGES:
        offset = POSTS_TABLE + image * 4
        width_bytes, height = rom[offset], rom[offset + 1]
        data = (rom[offset + 2] << 8) | rom[offset + 3]
        records[image] = (width_bytes, height, data)
    return records


def write_post_sprites(rom: bytes) -> list[str]:
    """The score posts as WHITE masks (they are only ever drawn as a solid colour)."""
    written = []
    for index, image in enumerate(POST_IMAGES, start=1):
        width_bytes, height, data = post_records(rom)[image]
        img = Image.new("RGBA", (width_bytes * 2, height), (0, 0, 0, 0))
        px = img.load()
        for y in range(height):
            for x in range(width_bytes * 2):
                byte = rom[data + y * width_bytes + (x >> 1)]
                nibble = (byte >> 4) & 0xF if x % 2 == 0 else byte & 0xF
                if nibble:
                    px[x, y] = (255, 255, 255, 255)
        name = f"AttractPost_{index}.png"
        img.save(SPRITES / name)
        written.append(f"{name} (image {image}, {width_bytes * 2}x{height})")
    return written


def main() -> int:
    rom = ROM.read_bytes()
    if len(rom) != ROM_SIZE:
        raise SystemExit(f"{ROM} is not a 64K image")

    # The large font's per-code width byte: the font table at $EC34 holds a
    # pointer per code (code - $30), and the pointer addresses a width-in-pixels
    # byte followed by the glyph rows.
    widths = []
    for code in range(0x30, 0x5F):
        ptr = (rom[FONT_TABLE + (code - 0x30) * 2] << 8) | rom[FONT_TABLE + (code - 0x30) * 2 + 1]
        widths.append(rom[ptr] if ptr else 0)

    parts = [
        "// <auto-generated />\n",
        "// GENERATED by tools/extract-attract-scripts.py from ref/rom/robotron64k.bin —\n",
        "// do not edit by hand. Regenerate after a ROM change.\n",
        "//\n",
        "// The arcade's attract MOVIE (notes §95/§96): the ROM's own HISTO page script\n",
        "// and object scripts, embedded verbatim so the port replays the arcade's\n",
        "// storyline instead of a transcription. Addresses are the R5 ones.\n",
        "\n",
        "namespace Robotron2084.Level.Attract;\n\n",
        "/// <summary>The ROM's attract-movie byte streams and tables (notes §95).</summary>\n",
        "public static class AttractMovieData\n{\n",
        f"    /// <summary>The R5 address of <see cref=\"Scripts\"/>[0] — object scripts address into it.</summary>\n",
        f"    public const int ScriptBase = 0x{SCRIPT_START:04X};\n\n",
        byte_array(
            "Histo", rom, HISTO_START, HISTO_END - HISTO_START,
            "The page script HISTO ($7FA3): the whole storyline, opcode by opcode, "
            "starting with the intro screen's text crawl."),
        "\n",
        byte_array(
            "Scripts", rom, SCRIPT_START, SCRIPT_END - SCRIPT_START,
            "The object scripts ($83B7..$878C): every character's choreography, "
            "addressed by <see cref=\"ScriptBase\"/>."),
        "\n",
        byte_array(
            "WalkHuman", rom, WALK_HUMAN, WALK_LENGTH,
            "HUMANA ($03CF): the family's walk table — 4 directions of four "
            "(image x4, dx pixels, dy pixels) steps plus a $FF guard."),
        "\n",
        byte_array(
            "WalkHulk", rom, WALK_HULK, WALK_LENGTH,
            "HLKANA ($01CC): the hulk's walk table, same layout as the family's."),
        "\n",
        byte_array(
            "AnimTable", rom, ANIM_TABLE, 4,
            "ANATAB ($7DEF): the walkers' 4-frame picture cycle (0,1,0,2)."),
        "\n",
        "    /// <summary>\n"
        "    /// The LARGE font's width-in-pixels for character codes $30..$5E, indexed\n"
        "    /// by (code - $30) — the ROM's font table ($EC34) entries point at a width\n"
        "    /// byte. BLIT_LARGE_CHARACTER advances the text pen by (width + 1) pixels\n"
        "    /// (notes §96.2), so the movie's text needs these to lay out like the\n"
        "    /// arcade's.\n"
        "    /// </summary>\n"
        "    public static readonly byte[] FontWidths =\n    [\n",
        "        " + " ".join(f"0x{w:02X}," for w in widths),
        "\n    ];\n",
        "\n",
        "    /// <summary>\n"
        "    /// The message strings the movie's MESS opcode prints beside a character,\n"
        "    /// decoded from the ROM's pointer table at $6377 — index 0 is message 115\n"
        "    /// (MOMMY) and index 11 is 126 (NULMES, the empty string that clears the\n"
        "    /// row). The numbering is RRFRED.ASM's (MMOM 115, MDAD 116, MKID 117,\n"
        "    /// MGRUNT 118, MHULK 119, MSPCU 120, MENTK 121, MBRAIN 122, COINMF 123,\n"
        "    /// MPROG 124, EXTMES 125, NULMES 126).\n"
        "    /// </summary>\n"
        "    public static readonly string[] Messages =\n    [\n",
        "".join(f"        \"{message(rom, n)}\",\n" for n in range(MESSAGE_FIRST, MESSAGE_FIRST + MESSAGE_COUNT)),
        "    ];\n",
        "\n",
        "    /// <summary>The ROM message number of <see cref=\"Messages\"/>[0].</summary>\n",
        f"    public const int FirstMessageNumber = {MESSAGE_FIRST};\n",
        "}\n",
    ]

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("".join(parts), encoding="utf-8")
    posts = write_post_sprites(rom)
    print(
        f"{OUT.relative_to(ROOT)}: HISTO {HISTO_END - HISTO_START} B, "
        f"scripts {SCRIPT_END - SCRIPT_START} B, "
        f"walk {WALK_LENGTH} B x2, font widths {len(widths)}, "
        f"messages {MESSAGE_COUNT}"
    )
    for line in posts:
        print("  " + line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
