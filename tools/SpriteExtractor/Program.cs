// SpriteExtractor — ports Robotron 2084 sprite bitmaps into 32-bit
// colour+alpha PNG assets under src/Robotron2084/Content/Sprites/.
//
// Sprite format: 4 bits per pixel, 2 pixels per byte, row by row.
// HIGH nibble = LEFT pixel, LOW nibble = RIGHT pixel.
// Each nibble is a 0-15 palette index.
//
// Blit modes (arcade blitter, author 2026-09-13 round 6: "make the sprites
// transparent where they should be"): palette slot 0 is the blitter's
// TRANSPARENT pixel for most sprites (a 0-nibble does not draw, so sprites
// no longer paint black boxes over the wall and each other). A few pictures
// are SOLID blits (every nibble draws, 0 = black) — the arcade uses this
// for the progs' phony-burst picture (PGXPIC); those names are listed in
// SolidNames and their 0-nibbles come out opaque.
//
// Palette: ported from the author's WmsGfxSpriteEditor
// RobotronPaletteService (WmsGfxSpriteEditor.Roms.Robotron2084/Shared/Palettes).
// That service credits Sean Riddle's Williams ripper (seanriddle.com/ripper.html)
// for the byte→RGB conversion. Slot colour bytes: 0-9 = ROM defaults at $DA51;
// 10-15 (colour-cycling in game) = the service's de-duplicated values so every
// slot has a distinct, identifiable RGB. No 6809 / Williams hardware modelling:
// the PNGs hold plain RGB per slot.
//
// Usage:
//   dotnet run --project tools/SpriteExtractor                                full run
//   dotnet run --project tools/SpriteExtractor -- --sprite NAME [NAME ...]    one or more
//   [--quiet] suppresses the ASCII previews (summary line per sprite only)
// NAME is a name from docs/robotronsprites.txt (e.g. "familydeath"),
// case-insensitive; prints the ASCII preview + PNG path for validation.
//
// Pass A (primary): the DEFINITIVE sprite source (author directive) is
//   C:\Users\scott\source\repos\WmsGfxSpriteEditor\WmsGfxSpriteEditor.Roms.Robotron2084\
//   Shared\Sprites\RobotronBlueLabelSpriteRepository.cs
// (_sprites.Add(new(id, name, offset, widthInBytes, height, BitsPerPixel));
//  id and BitsPerPixel ignored — bpp is always 4). docs/robotronsprites.txt
// is a committed mirror verified byte-identical to it (2026-09-06, 215
// entries: name, DECIMAL offset into the 64K ROM, width bytes, height px).
// Data is read straight from ref/rom/robotron64k.bin (byte-level authority).
// Do NOT scan/reverse-engineer the ROM for sprite locations (author directive).
//
// Pass B (fallback): original 6809 source listings (ref/original-source/*.ASM)
// for sprites Riddle's list does not cover. A frame is only emitted when its
// byte sequence is found verbatim in the ROM.
//
// Zero external dependencies; hand-rolled minimal 24-bit colour PNG writer.

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    static int Main(string[] args)
    {
        string repoRoot = FindRepoRoot();
        string romPath = Path.Combine(repoRoot, "ref/rom/robotron64k.bin");
        string listPath = Path.Combine(repoRoot, "docs/robotronsprites.txt");
        string pngDir = Path.Combine(repoRoot, "src/Robotron2084/Content/Sprites");
        string mapPath = Path.Combine(repoRoot, "docs/sprite-map.md");

        byte[] rom = File.ReadAllBytes(romPath);

        var spriteNames = new List<string>();
        bool quiet = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--sprite")
            {
                bool added = false;
                while (i + 1 < args.Length && args[i + 1].StartsWith("--") == false)
                {
                    spriteNames.Add(args[++i]);
                    added = true;
                }

                if (added == false)
                {
                    Console.WriteLine("usage: SpriteExtractor [--sprite NAME [NAME ...]] [--quiet]");
                    return 2;
                }

                continue;
            }

            if (args[i] == "--quiet")
            {
                quiet = true;
                continue;
            }

            Console.WriteLine("usage: SpriteExtractor [--sprite NAME [NAME ...]] [--quiet]");
            return 2;
        }

        if (spriteNames.Count > 0)
        {
            return RunSprites(rom, listPath, pngDir, spriteNames, quiet);
        }

        Directory.CreateDirectory(pngDir);

        var map = new StringBuilder();
        map.AppendLine("# Sprite map — extracted from robotron64k.bin (Release 5 solid blue)");
        map.AppendLine();
        map.AppendLine("24-bit colour PNGs at 1:1 arcade pixel size (4 bits/pixel; high nibble = left pixel).");
        map.AppendLine("Palette per WmsGfxSpriteEditor RobotronPaletteService (seanriddle.com ripper conversion).");
        map.AppendLine();
        map.AppendLine("- **Pass A**: offsets from the definitive sprite source (author directive) —");
        map.AppendLine("  `WmsGfxSpriteEditor.../Shared/Sprites/RobotronBlueLabelSpriteRepository.cs`");
        map.AppendLine("  (215 entries); `docs/robotronsprites.txt` is a committed mirror verified identical.");
        map.AppendLine("  Offsets are decimal into `ref/rom/robotron64k.bin`; width in bytes, height in px.");
        map.AppendLine("- **Pass B**: frames located in the original source listings, emitted only when the");
        map.AppendLine("  byte sequence is found verbatim in the ROM.");
        map.AppendLine();
        map.AppendLine("| name | source | ROM offset | size (px) | PNG | notes |");
        map.AppendLine("|------|--------|------------|-----------|-----|-------|");

        int written = 0;
        var warnings = new List<string>();

        // ---------------- Pass A: Riddle's list, straight from the ROM.
        foreach (string raw in File.ReadAllLines(listPath))
        {
            string line = raw.TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            // name (col 1), offset decimal (col 2), width bytes (col 3), height px (col 4).
            int nameEnd = 0;
            while (nameEnd < line.Length && char.IsWhiteSpace(line[nameEnd]) == false)
            {
                nameEnd++;
            }

            string name = line[..nameEnd];
            var fields = line[nameEnd..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 4)
            {
                continue;
            }

            if (!int.TryParse(fields[0], out int off) || !int.TryParse(fields[1], out int w) || !int.TryParse(fields[2], out int h))
            {
                warnings.Add($"list: bad row: '{line}'");
                continue;
            }

            if (w < 1 || h < 1 || w * h > rom.Length || off < 0 || off + w * h > rom.Length)
            {
                warnings.Add($"list: out of range: {name} off={off} {w}x{h}");
                continue;
            }

            byte[] data = rom[off..(off + w * h)];
            string png = MapNameA(name);
            if (IsFontGlyph(png))
            {
                continue;
            }

            WritePng(Path.Combine(pngDir, png + ".png"), w * 2, h, data, w, SolidNames.Contains(png));
            written++;
            map.AppendLine($"| {name} | riddle list | ${off:X4} | {w * 2}×{h} | {png}.png | {(SolidNames.Contains(png) ? "solid blit" : "")} |");
        }

        // ---------------- Pass C: ROM-located extras (raw frame data at known offsets).
        // These frames are in the ROM but not in Riddle's list and differ from the
        // source listing (which predates R5 — author 2026-09-12: "the source is a
        // little out of date"). Offsets are the *frame data* addresses.
        (string Name, int Off, int W, int H, byte[]? Inline)[] extras =
        {
            // Tank shell — author-confirmed 2026-09-12: raw pixel data at $4FF2,
            // 7 bytes wide x 16 rows = 14x16 px (notes §11.5). The sprite metadata
            // lives elsewhere and points here; we only need the pixels.
            // Tank shell — offset confirmed by the author's own sprite repository
            // ("tankshell", 20466 = $4FF2, 4 bytes x 7 rows), and the pre-R5
            // source agrees (`RRTK4` `SHLP1 FCB 4,7`). NOTES §53: the port used to
            // extract 7x16 from here, a 224-byte window that swallowed whatever
            // follows the shell. `SHLD1`'s bytes in the source are NOT the ROM's
            // (the source predates R5), so the repository's size is the authority.
            ("TankShell", 0x4FF2, 4, 7, null),
            // Tank BIRTH frames (notes §52/§53) — the ROM's `MTNKP1..4`
            // descriptors point at this pixel data, which is CONTIGUOUS and
            // byte-identical to the pre-R5 source's `MTNKD1..4`: 4x4, 8x7, 8x8
            // and 12x12 px. `MTANK` plays one per `NAP 12`, shifting the object
            // by that descriptor's (dx,dy) each step, until the pointer reaches
            // `TNKP1` (the 14x16 tank) — so a dropped tank GROWS instead of
            // appearing full size. Pass B could not reach these (it cannot parse
            // the 6-byte animated descriptors), but the DATA is not missing.
            ("TankGrow_1", 0x5036, 2, 4, null),
            ("TankGrow_2", 0x503E, 4, 7, null),
            ("TankGrow_3", 0x505A, 4, 8, null),
            ("TankGrow_4", 0x507A, 6, 12, null),
            // Prog phony-burst (RRB10 PGXPIC/PGXD) — 6 bytes x 16 rows = 12x16,
            // SOLID blit. The pre-R5 source's bytes are NOT verbatim in the R5
            // ROM (R5 re-drew the picture; a byte search for the PGXD pattern
            // finds nothing), and locating R5's replacement would mean
            // reverse-engineering sprite pointers (author directive: don't).
            // We keep the original source's burst as the port's phony burst —
            // same shape/size, correct blit mode.
            //
            // ⚠ These are PGXD's 16 `FDB` rows VERBATIM — six bytes a row. The
            // first version of this array mis-transcribed the middle rows as
            // EIGHT bytes (it split `FDB $AA00,$0000,$0AA0` into four 16-bit
            // values), so the writer — which reads `w` = 6 bytes per row — walked
            // a misaligned stream: the PNG decoded to noise, and the author saw
            // it as the prog's "explosion effect looks weird" (2026-09-17).
            ("ProgBurst", 0, 6, 16, new byte[]
            {
                0xAA,0xAA, 0xAA,0xAA, 0xAA,0xA0, // $AAAA,$AAAA,$AAA0
                0xAA,0x00, 0x00,0x00, 0x0A,0xA0, // $AA00,$0000,$0AA0
                0xAA,0x0B, 0xB0,0xBB, 0x0A,0xA0, // $AA0B,$B0BB,$0AA0
                0xAA,0x0B, 0xB0,0xBB, 0x0A,0xA0, // $AA0B,$B0BB,$0AA0
                0xAA,0x0B, 0xB0,0xBB, 0x0A,0xA0, // $AA0B,$B0BB,$0AA0
                0xAA,0x00, 0x00,0x00, 0x0A,0xA0, // $AA00,$0000,$0AA0
                0xAA,0xAA, 0xA0,0xAA, 0xAA,0xA0, // $AAAA,$A0AA,$AAA0
                0xAA,0xA0, 0x00,0x00, 0xAA,0xA0, // $AAA0,$0000,$AAA0
                0xAA,0x00, 0x00,0x00, 0x0A,0xA0, // $AA00,$0000,$0AA0
                0xAA,0x0A, 0x00,0x0A, 0x0A,0xA0, // $AA0A,$000A,$0AA0
                0xAA,0x0A, 0x00,0x0A, 0x0A,0xA0, // $AA0A,$000A,$0AA0
                0xAA,0xAA, 0x0A,0x0A, 0xAA,0xA0, // $AAAA,$0A0A,$AAA0
                0xAA,0xAA, 0x0A,0x0A, 0xAA,0xA0, // $AAAA,$0A0A,$AAA0
                0xAA,0xAA, 0x0A,0x0A, 0xAA,0xA0, // $AAAA,$0A0A,$AAA0
                0xAA,0x00, 0x0A,0x00, 0x0A,0xA0, // $AA00,$0A00,$0AA0
                0xAA,0xAA, 0xAA,0xAA, 0xAA,0xA0, // $AAAA,$AAAA,$AAA0
            }),
        };
        foreach ((string name, int off, int w, int h, byte[]? inline) in extras)
        {
            byte[] data;
            if (inline is not null)
            {
                // The writer reads ONE ROW per `bytesPerRow` = w bytes, so inline data must
                // be exactly w*h bytes. A LONGER array is not caught downstream — the writer
                // just walks a misaligned stream and emits a plausible-looking PNG of noise
                // (which is how the prog burst art came out wrong: the middle rows of the
                // ProgBurst array were transcribed as eight bytes instead of six).
                if (inline.Length != w * h)
                {
                    warnings.Add($"extra {name}: inline data is {inline.Length} bytes, expected {w * h} ({w}x{h})");
                    continue;
                }

                data = inline;
            }
            else if (off < 0 || off + w * h > rom.Length)
            {
                warnings.Add($"extra out of range: {name}");
                continue;
            }
            else
            {
                data = rom[off..(off + w * h)];
            }
            string png = name;
            if (IsFontGlyph(png))
            {
                continue;
            }

            WritePng(Path.Combine(pngDir, png + ".png"), w * 2, h, data, w, SolidNames.Contains(png));
            written++;
            string src = inline is not null ? "source data" : $"${off:X4}";
            map.AppendLine($"| {name} | rom chase | {src} | {w * 2}×{h} | {png}.png | pass C {(SolidNames.Contains(png) ? "solid blit" : "")} |");
        }

        // ---------------- Pass B: source listings, ROM-verified only.
        string[] sourceDir = { Path.Combine(repoRoot, "ref/original-source") };
        string[] exclude = { "RRTEST1", "RRTEST2", "RRTESTB", "RRTESTC", "JAPDATA", "RRELESE6" };
        string[] files = Directory.GetFiles(sourceDir[0], "*.ASM")
            .Where(f => exclude.All(x => !Path.GetFileNameWithoutExtension(f).StartsWith(x)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        var emitted = new HashSet<string>();
        var seenData = new HashSet<string>();
        foreach (string file in files)
        {
            string fname = Path.GetFileNameWithoutExtension(file);
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                (int Width, int Height, string Label)? header = TryParseHeader(lines[i]);
                if (header is null)
                {
                    continue;
                }

                (int width, int height, string label) = header.Value;

                // Pass B only supplements Pass A: skip labels already covered.
                if (!WantedB(label))
                {
                    continue;
                }

                int expected = width * height;
                byte[]? data = CollectData(lines, i, expected);
                if (data is null)
                {
                    warnings.Add($"{fname}:{i + 1} {label}: could not collect {expected} bytes");
                    continue;
                }

                if (seenData.Contains(label))
                {
                    continue;
                }

                seenData.Add(label);
                int off = FindBytes(rom, data);
                if (off < 0)
                {
                    warnings.Add($"{fname}:{i + 1} {label}: NOT FOUND in ROM (source/ROM discrepancy) — skipped");
                    continue;
                }

                string png = MapNameB(label);
                if (emitted.Contains(png))
                {
                    png += "_" + label;
                }

                if (IsFontGlyph(png))
                {
                    continue;
                }

                emitted.Add(png);
                WritePng(Path.Combine(pngDir, png + ".png"), width * 2, height, data, width, SolidNames.Contains(png));
                written++;
                map.AppendLine($"| {label} | source {fname} | ${off:X4} | {width * 2}×{height} | {png}.png | pass B (ROM-verified) {(SolidNames.Contains(png) ? "solid blit" : "")} |");
            }
        }

        File.WriteAllText(mapPath, map.ToString());

        Console.WriteLine($"PNGs written: {written} -> {pngDir}");
        Console.WriteLine($"map:         {mapPath}");
        if (warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"WARNINGS ({warnings.Count}):");
            foreach (string w in warnings)
            {
                Console.WriteLine("  " + w);
            }
        }

        return 0;
    }

    // ------------------------------------------------------------------
    // Sprite validation mode: extract one or more sprites from Riddle's
    // list, write their PNGs, and print ASCII previews so a human can
    // validate them (--quiet: summary lines only).

    static int RunSprites(byte[] rom, string listPath, string pngDir, List<string> names, bool quiet)
    {
        string[] listLines = File.ReadAllLines(listPath);
        int missing = 0;
        foreach (string name in names)
        {
            if (ExtractOne(rom, listLines, pngDir, name, quiet) == false)
            {
                Console.WriteLine($"not found in {listPath}: '{name}'");
                missing++;
            }
        }

        return missing == 0 ? 0 : 1;
    }

    static bool ExtractOne(byte[] rom, string[] listLines, string pngDir, string name, bool quiet)
    {
        foreach (string raw in listLines)
        {
            string line = raw.TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            int nameEnd = 0;
            while (nameEnd < line.Length && char.IsWhiteSpace(line[nameEnd]) == false)
            {
                nameEnd++;
            }

            string rowName = line[..nameEnd];
            if (rowName.Equals(name, StringComparison.OrdinalIgnoreCase) == false &&
                MapNameA(rowName).Equals(name, StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            var fields = line[nameEnd..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 4 ||
                !int.TryParse(fields[0], out int off) ||
                !int.TryParse(fields[1], out int w) ||
                !int.TryParse(fields[2], out int h))
            {
                continue;
            }

            if (w < 1 || h < 1 || w * h > rom.Length || off < 0 || off + w * h > rom.Length)
            {
                Console.WriteLine($"{rowName}: out of range (off={off} {w}x{h})");
                return false;
            }

            byte[] data = rom[off..(off + w * h)];
            string pngName = MapNameA(rowName);
            Directory.CreateDirectory(pngDir);
            string pngPath = Path.Combine(pngDir, pngName + ".png");
            WritePng(pngPath, w * 2, h, data, w, SolidNames.Contains(pngName));

            if (quiet)
            {
                Console.WriteLine($"{rowName} -> {pngName}.png  ${off:X4}  {w * 2}x{h}");
                return true;
            }

            Console.WriteLine($"{rowName} -> {pngName}.png");
            Console.WriteLine($"  ROM offset: ${off:X4} ({off})   size: {w * 2}x{h} px ({w} bytes x {h} rows)");
            Console.WriteLine($"  wrote: {pngPath}");
            Console.WriteLine("  ASCII (each char = palette slot; high nibble = left pixel):");
            for (int y = 0; y < h; y++)
            {
                var sb = new StringBuilder(2 * w);
                for (int x = 0; x < 2 * w; x++)
                {
                    byte b = data[y * w + (x >> 1)];
                    int nibble = (x & 1) == 0 ? (b >> 4) & 0xF : b & 0xF;
                    sb.Append(AsciiGlyph(nibble));
                }

                Console.WriteLine($"   {y,2}  {sb}");
            }

            Console.WriteLine("  legend: " + string.Join(" ", Enumerable.Range(0, 16).Select(i => $"{AsciiGlyph(i)}={Palette.Rgb[i]}")));
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------
    // Palette — ported from WmsGfxSpriteEditor RobotronPaletteService.
    // Credit to Sean Riddle for the byte→RGB conversion
    // (https://www.seanriddle.com/ripper.html).

    static class Palette
    {
        // Robotron ROM colour bytes per 16-slot palette. 0-9 = ROM defaults
        // ($DA51); 10-15 = the service's de-duplicated values for the
        // in-game colour-cycling slots (duplicates would make a texel's slot
        // ambiguous).
        private static readonly byte[] ColorValues =
        [
            0x00, 0x07, 0x17, 0xC7, 0x1F, 0x3F, 0x38, 0xC0,
            0xA4, 0xFF, 0xC4, 0xF4, 0xCC, 0x81, 0x45, 0x2F,
        ];

        public static readonly Rgb[] Rgb = [.. ColorValues.Select(FromColorValue)];

        // Port of RobotronPaletteService.ConvertColorValue (byte = BBGGGRRR):
        // R = bits 0-2 (x2, +1 if >6), G = bits 3-5 (+1 if >6), B = bits 6-7 (x5),
        // each then x16 (clamped to 255).
        private static Rgb FromColorValue(byte value)
        {
            int red = (value & 0x07) << 1;
            if (red > 6)
            {
                red++;
            }

            int green = (value & 0x38) >> 2;
            if (green > 6)
            {
                green++;
            }

            int blue = ((value & 0xC0) >> 6) * 5;

            return new Rgb(
                (byte)Math.Min(255, red << 4),
                (byte)Math.Min(255, green << 4),
                (byte)Math.Min(255, blue << 4));
        }
    }

    internal readonly record struct Rgb(byte R, byte G, byte B)
    {
        public override string ToString() => $"({R},{G},{B})";
    }

    // ------------------------------------------------------------------
    // Pass B helpers (source-listing parsing).

    // NOTE: `MTNKP1..4` are deliberately absent — Pass B cannot parse their
    // 6-byte animated descriptors (it reported "could not collect 8 bytes" /
    // "NOT FOUND in ROM"), and their frame data IS in the ROM: Pass C carries
    // `MTNKD1..4` at $5036/$503E/$505A/$507A. Ask for them there, not here.
    static bool WantedB(string label) =>
        label is "SHLP1"
            or "CMPIC" or "CMP1" or "MNPIC" or "NULLP" or "CRUSB"; // PGXPIC: pass C (R5 re-drew it)

    static (int Width, int Height, string Label)? TryParseHeader(string line)
    {
        Match m = Regex.Match(line, @"^\s*([A-Z][A-Z0-9]{0,9})\s+FCB\s+([0-9]{1,2}),([0-9]{1,2})(?=\s|$)");
        if (!m.Success)
        {
            return null;
        }

        int w = int.Parse(m.Groups[2].Value);
        int h = int.Parse(m.Groups[3].Value);
        if (w < 1 || w > 16 || h < 1 || h > 32)
        {
            return null;
        }

        return (w, h, m.Groups[1].Value);
    }

    /// <summary>Collects w*h bytes for a header at lines[hi]. Handles the optional
    /// `FDB symbol` pointer (+ delta line) then data from the symbol definition.</summary>
    static byte[]? CollectData(string[] lines, int hi, int expected)
    {
        int j = hi + 1;
        string? symbol = null;
        if (j < lines.Length)
        {
            string[] t = Tokenize(lines[j]);
            if (t.Length == 2 && t[0] == "FDB" && IsSymbol(t[1]))
            {
                symbol = t[1];
                j++;
            }
        }

        if (symbol is not null)
        {
            while (j < lines.Length && Tokenize(lines[j]).FirstOrDefault() != symbol)
            {
                j++;
            }

            if (j >= lines.Length)
            {
                return null;
            }
        }

        List<byte> bytes = new(expected);
        for (; j < lines.Length; j++)
        {
            int[]? values = DataValues(lines[j], out bool isFdb);
            if (values is null)
            {
                continue;
            }

            foreach (int v in values)
            {
                if (isFdb)
                {
                    bytes.Add((byte)(v & 0xFF));
                    bytes.Add((byte)((v >> 8) & 0xFF));
                }
                else
                {
                    bytes.Add((byte)(v & 0xFF));
                }

                if (bytes.Count == expected)
                {
                    return bytes.ToArray();
                }
            }

            // Stop if we drift into another sprite header.
            if (TryParseHeader(lines[j]) is not null)
            {
                break;
            }
        }

        return null;
    }

    static int[]? DataValues(string line, out bool isFdb)
    {
        isFdb = false;
        string[] t = Tokenize(line);
        if (t.Length == 0)
        {
            return null;
        }

        int start;
        if (t[0] is "FCB" or "FDB")
        {
            isFdb = t[0] == "FDB";
            start = 1;
        }
        else if (t.Length >= 2 && t[1] is "FCB" or "FDB")
        {
            isFdb = t[1] == "FDB";
            start = 2;
        }
        else
        {
            return null;
        }

        List<int> values = new();
        foreach (string token in t.Skip(start))
        {
            foreach (string part in token.Split(','))
            {
                if (!IsHex(part) && !IsDecimal(part))
                {
                    goto done;
                }

                values.Add(ParseValue(part));
            }
        }

    done:
        return values.Count > 0 ? values.ToArray() : null;
    }

    static string[] Tokenize(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static bool IsSymbol(string token) =>
        token.Length > 0 && token.Length <= 10 && char.IsUpper(token[0]) && token.All(c => char.IsUpper(c) || char.IsDigit(c));

    static bool IsHex(string token) =>
        token.Length >= 2 && token[0] == '$' && token.Skip(1).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    static bool IsDecimal(string token) => token.Length >= 1 && token.All(char.IsDigit);

    static int ParseValue(string token) => token[0] == '$' ? Convert.ToInt32(token[1..], 16) : int.Parse(token);

    static int FindBytes(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length)
        {
            return -1;
        }

        byte first = needle[0];
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack[i] != first)
            {
                continue;
            }

            bool ok = true;
            for (int k = 1; k < needle.Length; k++)
            {
                if (haystack[i + k] != needle[k])
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                return i;
            }
        }

        return -1;
    }

    // ------------------------------------------------------------------
    // Naming.

    static string MapNameA(string name)
    {
        if (name == "familydeath")
        {
            return "Skull";
        }

        if (name.Length == 4 && name.All(char.IsDigit))
        {
            return "Score_" + name;
        }

        return name switch
        {
            _ when name.StartsWith("mommy") => "Mummy" + Suffix(name, "mommy"),
            _ when name.StartsWith("daddy") => "Daddy" + Suffix(name, "daddy"),
            _ when name.StartsWith("mikey") => "Mikey" + Suffix(name, "mikey"),
            _ when name.StartsWith("hulk") => "Hulk" + Suffix(name, "hulk"),
            _ when name.StartsWith("sphereoid") => "Spheroid" + Suffix(name, "sphereoid"),
            _ when name.StartsWith("enforcerbullet") => "Spark" + Suffix(name, "enforcerbullet"),
            _ when name.StartsWith("enforcer") => "Enforcer" + Suffix(name, "enforcer"),
            _ when name == "player" => "PlayerBig",
            _ when name.StartsWith("brain") => "Brain" + Suffix(name, "brain"),
            _ when name.StartsWith("player") => "Player" + Suffix(name, "player"),
            _ when name.StartsWith("electrode") => "Electrode" + Suffix(name, "electrode"),
            _ when name.StartsWith("grunt") => "Grunt" + Suffix(name, "grunt"),
            _ when name.StartsWith("quark") => "Quark" + Suffix(name, "quark"),
            _ when name.StartsWith("tank") => "Tank" + Suffix(name, "tank"),
            _ when name.StartsWith("smallfont") => "Font_S_" + name["smallfont".Length..],
            _ when name == "largefont:" => "Font_L_colon", // ':' is illegal in Windows file names
            _ when name.StartsWith("largefont") => "Font_L_" + name["largefont".Length..],
            _ => name,
        };

        static string Suffix(string name, string prefix)
        {
            string num = name[prefix.Length..];
            return num.Length > 0 ? "_" + num : "";
        }
    }

    // Solid-blit pictures: the arcade blits these with EVERY nibble drawn
    // (slot 0 = opaque black) — currently only the prog's phony-burst
    // picture (PGXPIC, RRB10). Everything else is transparent-blit (slot 0
    // = no pixel).
    static readonly HashSet<string> SolidNames = new() { "ProgBurst" };

    /// <summary>
    /// Font glyphs are NOT this tool's output. <c>tools/extract-fonts.py</c>
    /// owns them: it writes white-on-transparent masters (the port tints them
    /// with the live palette slot at draw time) plus the six colour-cycle
    /// MARKER variants the M4 shader needs, which this tool knows nothing
    /// about — and this tool writes palette-coloured pixels instead, which
    /// would break both paths. Two writers also hid a real bug: the Python
    /// script had the two pixels of every byte swapped (notes §60) while a run
    /// of this one was "reverted" as the suspicious change.
    /// </summary>
    static bool IsFontGlyph(string png) => png.StartsWith("Font_L_", StringComparison.Ordinal)
        || png.StartsWith("Font_S_", StringComparison.Ordinal);

    static string MapNameB(string label) => label switch
    {
        "SHLP1" => "TankShell",
        "PGXPIC" => "ProgBurst",
        "MTNKP1" => "TankGrow_1",
        "MTNKP2" => "TankGrow_2",
        "MTNKP3" => "TankGrow_3",
        "MTNKP4" => "TankGrow_4",
        "CMPIC" => "MissileSmall_0",
        "CMP1" => "MissileSmall_1",
        "MNPIC" => "PlayerExtra",
        "NULLP" => "Dot",
        "CRUSB" => "AttractCruise",
        _ => label,
    };

    // ------------------------------------------------------------------
    // ASCII preview.

    static char AsciiGlyph(int slot)
    {
        // '0'-'9' then 'A'-'F' — matches the hex nibble notation used in the
        // notes, so rows can be cross-checked against byte dumps.
        return slot < 10 ? (char)('0' + slot) : (char)('A' + (slot - 10));
    }

    // ------------------------------------------------------------------
    // PNG writer (32-bit colour+alpha, colour type 6). Slot 0 is transparent
    // (alpha 0) unless <paramref name="solid"/> (solid blit: slot 0 = opaque
    // black) — see SolidNames.

    static void WritePng(string path, int width, int height, byte[] data, int bytesPerRow, bool solid)
    {
        byte[] raw = new byte[height * (1 + width * 4)];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * (1 + width * 4);
            raw[rowStart] = 0; // filter: none
            for (int x = 0; x < width; x++)
            {
                byte b = data[y * bytesPerRow + (x >> 1)];
                int nibble = (x & 1) == 0 ? (b >> 4) & 0xF : b & 0xF;
                Rgb c = Palette.Rgb[nibble];
                raw[rowStart + 1 + x * 4] = c.R;
                raw[rowStart + 2 + x * 4] = c.G;
                raw[rowStart + 3 + x * 4] = c.B;
                raw[rowStart + 4 + x * 4] = nibble == 0 && solid ? (byte)0xFF : (byte)(nibble == 0 ? 0 : 0xFF);
            }
        }

        using FileStream fs = File.Create(path);
        fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(fs, "IHDR", Ihdr(width, height));
        WriteChunk(fs, "IDAT", ZlibCompress(raw));
        WriteChunk(fs, "IEND", Array.Empty<byte>());
    }

    static byte[] Ihdr(int w, int h) => new byte[]
    {
        (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w,
        (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h,
        8, 6, 0, 0, 0, // bit depth 8, colour type 6 (truecolour RGBA), compression 0, filter 0, interlace 0
    };

    static byte[] ZlibCompress(byte[] data)
    {
        using MemoryStream ms = new();
        using (ZLibStream zs = new(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            zs.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }

    static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        len[0] = (byte)(data.Length >> 24);
        len[1] = (byte)(data.Length >> 16);
        len[2] = (byte)(data.Length >> 8);
        len[3] = (byte)data.Length;
        s.Write(len);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        byte[] crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        Span<byte> crc = stackalloc byte[4];
        uint c = Crc32(crcInput);
        crc[0] = (byte)(c >> 24);
        crc[1] = (byte)(c >> 16);
        crc[2] = (byte)(c >> 8);
        crc[3] = (byte)c;
        s.Write(crc);
    }

    static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint x = n;
            for (int k = 0; k < 8; k++)
            {
                x = (x & 1) != 0 ? 0xEDB88320 ^ (x >> 1) : x >> 1;
            }

            table[n] = x;
        }

        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    static string FindRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ref")) && Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found from " + Environment.CurrentDirectory);
    }
}
