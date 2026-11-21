#!/usr/bin/env python3
"""Makes a new word for the Berzerk robot that sounds like the Berzerk speech chip said it.

The chip's own words were made by recording a person and squeezing the recording into the chip's tiny format. This tool
does the same to a new word:

  * Windows' built-in "Microsoft David" voice says the word, or you give your own recording with --wav.
  * The voice is made one flat note (93.9 Hz), the note every Berzerk word is said on.
  * Each beat of that note (128 steps of the chip's output) gets the sound's tone colour. The first half of the beat is
    silent; the second half is a short wave followed by the same wave played backwards. The chip's words are built
    exactly like this.
  * Hissing sounds (f, s, h) fill the whole beat with quiet noise instead, as the chip's own hissing sounds do.
  * The wave is drawn with the chip's own small steps up and down, on its 16 output levels.
  * Each beat is played three times, as the chip plays its own.

The tone and loudness settings live in tools/encode-berzerk-word.json. They were fitted so the new words match the
chip's real words, and they are the settings that made family.ogg: with them, this tool makes family.ogg again exactly.
--calibrate fits them again and replaces them, so words made afterwards will not sound quite the same as family.ogg.

Needs Windows (for the voice) and: pip install numpy scipy soundfile

Run from the repository root:
  python tools/encode-berzerk-word.py family            writes src/Robotron2084/Content/Speech/Berzerk/family.ogg
  python tools/encode-berzerk-word.py family --out x.ogg
  python tools/encode-berzerk-word.py family --wav me-saying-family.wav
  python tools/encode-berzerk-word.py --calibrate       fits the settings again from the real words
Then run tools/generate-mgcb.py and add the word to BerzerkWord.
"""
import argparse
import json
import os
import pathlib
import subprocess
import sys
import tempfile

import numpy as np
import soundfile as sf
from scipy.linalg import solve_toeplitz
from scipy.signal import lfilter, resample_poly

ROOT = pathlib.Path(__file__).resolve().parents[1]
BERZERK = ROOT / "src/Robotron2084/Content/Speech/Berzerk"
SETTINGS = pathlib.Path(__file__).with_suffix(".json")

# Words in the Berzerk folder that the chip never said. They are left out when fitting the settings.
NOT_FROM_THE_CHIP = {"humanoids", "humans", "family"}

# Real chip words that the calibration has Windows say too, so the two can be compared.
CALIBRATION_WORDS = ["humanoid", "kill", "fight", "help", "robot", "intruder", "alert", "chicken", "destroy", "escape",
                     "detected", "coins", "charge", "must", "attack", "like", "pocket", "shoot"]

SAMPLE_RATE = 24038          # the samples' rate: the chip's speed at pitch 3
SAMPLES_PER_STEP = 2         # the chip changes its output every 2 samples
STEP_RATE = SAMPLE_RATE / SAMPLES_PER_STEP
BEAT = 128                   # output steps in one beat of the chip's note
HALF = BEAT // 2
NOTE = STEP_RATE / BEAT      # 93.9 Hz
REPEATS = 3                  # times each beat is played
LEAD = 150 + 5 * BEAT * SAMPLES_PER_STEP   # silence before the word, as in the chip's words (about 60 ms)
TAIL = 2100                  # silence after the word (about 87 ms)
TONE_DETAIL = 12             # how finely the voice's tone colour is followed

# The chip's step rule. Each step is chosen by a 2-bit code, and how far it moves depends on the code before it.
# STEPS[code][code before] is the move, in output levels. A move of +3 can never follow a move of -3, or the other way.
STEPS = np.array([[-3, -3, -1, -1], [-1, -1, 0, 0], [0, 0, 1, 1], [1, 1, 3, 3]])
LEVELS = np.arange(-7, 9)    # the chip's 16 output levels; 0 is silence
LEVEL_SIZE = 7.5             # one level is 1/7.5 of full volume in the samples

NOISE = np.random.default_rng(2084)   # the same noise every run, so a word comes out the same every time


# ---------------------------------------------------------------- the chip's steps

def draw_with_chip_steps(target):
    """The run of chip steps whose output is closest to target (in output levels), starting from silence."""
    n, count = len(target), len(LEVELS)
    cost = np.full((count, 4), np.inf)
    cost[7, :] = 0.0                                  # level 0 is LEVELS[7]
    back = []
    for t in range(n):
        new = np.full((count, 4), np.inf)
        came_from = np.zeros((count, 4, 2), dtype=int)
        for li in range(count):
            for before in range(4):
                if cost[li, before] == np.inf:
                    continue
                for code in range(4):
                    level = min(8, max(-7, LEVELS[li] + STEPS[code, before]))
                    lj = level + 7
                    c = cost[li, before] + (level - target[t]) ** 2
                    if c < new[lj, code]:
                        new[lj, code] = c
                        came_from[lj, code] = (li, before)
        back.append(came_from)
        cost = new
    li, code = np.unravel_index(np.argmin(cost), cost.shape)
    out = np.zeros(n, dtype=int)
    for t in range(n - 1, -1, -1):
        out[t] = LEVELS[li]
        li, code = back[t][li, code]
    return out


# ---------------------------------------------------------------- reading the chip's real words

def read_beats(path):
    """A chip word as output levels, one per step, starting at the first silent half-beat."""
    x, _ = sf.read(path)
    if x.ndim > 1:
        x = x.mean(axis=1)
    levels = np.round(x * LEVEL_SIZE).astype(int)
    span = BEAT * SAMPLES_PER_STEP
    start = max(range(0, span, SAMPLES_PER_STEP),
                key=lambda o: sum((levels[o + k * span:o + k * span + HALF * SAMPLES_PER_STEP] == 0).sum()
                                  for k in range(len(levels) // span - 1)))
    steps = levels[start::SAMPLES_PER_STEP]
    return [steps[k * BEAT:(k + 1) * BEAT] for k in range(len(steps) // BEAT)]


def is_voiced(beat):
    """A buzzing beat: silent first half, sound in the second."""
    return np.abs(beat[:HALF]).sum() <= 8 and np.abs(beat[HALF:]).sum() >= 20


def tone_of(beat):
    """How a beat's loudness is shared out over the pitches, adding up to 1."""
    p = np.abs(np.fft.rfft(beat.astype(float))) ** 2
    return p / p.sum()


# ---------------------------------------------------------------- the recording

def say_with_windows(word, wav):
    """Has Windows' Microsoft David voice say the word into a WAV file."""
    script = (
        "Add-Type -AssemblyName System.Speech;"
        "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;"
        "$s.SelectVoice('Microsoft David Desktop'); $s.Rate = -2;"
        "$f = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050,"
        " [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono);"
        "$s.SetOutputToWaveFile($env:BERZERK_WAV, $f); $s.Speak($env:BERZERK_WORD); $s.Dispose()")
    env = dict(os.environ, BERZERK_WORD=word, BERZERK_WAV=str(wav))
    subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", script], check=True, env=env)


def read_recording(path):
    """The recording at the chip's step rate, with the silence before and after cut off."""
    x, rate = sf.read(path)
    if x.ndim > 1:
        x = x.mean(axis=1)
    y = resample_poly(x, 12019, rate)
    loudness = np.convolve(np.abs(y), np.ones(120) / 120, mode="same")
    sounding = np.where(loudness > 0.004 * loudness.max())[0]
    return y[sounding[0]:sounding[-1]]


# ---------------------------------------------------------------- listening to the recording

def tone_filter(frame):
    """A filter with the frame's tone colour (linear prediction), and how loud its input must be."""
    r = np.correlate(frame, frame, "full")[len(frame) - 1:len(frame) + TONE_DETAIL]
    if r[0] <= 1e-9:
        return np.r_[1.0, np.zeros(TONE_DETAIL)], 0.0
    r[0] *= 1.0001
    a = np.r_[1.0, solve_toeplitz(r[:TONE_DETAIL], -r[1:TONE_DETAIL + 1])]
    return a, np.sqrt(max(r[0] + a[1:] @ r[1:TONE_DETAIL + 1], 1e-12))


def repeats_itself(frame):
    """How closely the frame matches itself one voice beat later: 0 (not at all) to 1 (exactly)."""
    f = frame - frame.mean()
    best = 0.0
    for lag in range(int(STEP_RATE / 300), int(STEP_RATE / 70)):
        a, b = f[:-lag], f[lag:]
        d = np.sqrt((a @ a) * (b @ b))
        if d > 0:
            best = max(best, float(a @ b / d))
    return best


def listen(y, count):
    """Tone colour, loudness and whether it buzzes, for count moments spread evenly over the word."""
    width = int(0.040 * STEP_RATE)
    moments = []
    for centre in np.linspace(width / 2, len(y) - width / 2, count):
        start = int(centre - width / 2)
        frame = y[start:start + width]
        frame = np.pad(frame, (0, width - len(frame)))
        a, g = tone_filter(frame * np.hamming(width))
        power = np.abs(np.fft.rfft(frame * np.hanning(width))) ** 2
        freq = np.fft.rfftfreq(width, 1 / STEP_RATE)
        low = float(power[(freq > 60) & (freq < 1200)].sum() / (power[freq > 60].sum() + 1e-12))
        # A buzzing sound has most of its sound low down. The David voice creaks as its note falls, so how well it
        # repeats itself is only asked when that test is not clear.
        buzz = 1.0 if low > 0.5 else repeats_itself(frame)
        moments.append({"a": a, "g": g, "rms": float(np.sqrt(np.mean(frame ** 2))), "buzz": buzz, "low": low})
    return moments


# ---------------------------------------------------------------- making the chip's beats

def voiced_beat(a, g, tone, rms):
    """One buzzing beat: silent half, then a wave and the same wave backwards."""
    k = np.arange(1, HALF)
    w = 2 * np.pi * k * NOTE / STEP_RATE
    strength = g / np.abs(np.exp(-1j * np.outer(w, np.arange(len(a)))) @ a) * tone[k]
    # Every pitch peaks together in the middle of the second half, so that half is the same forwards and backwards.
    n = np.arange(BEAT)
    wave = (strength[:, None] * np.cos(2 * np.pi * np.outer(k, n - 95.5) / BEAT)).sum(axis=0)[HALF:]
    wave = wave / (np.sqrt(np.mean(wave ** 2)) + 1e-12) * rms
    quarter = draw_with_chip_steps((wave[:HALF // 2] + wave[::-1][:HALF // 2]) / 2)
    return np.concatenate([np.zeros(HALF, dtype=int), quarter, quarter[::-1]])


def hissing_beat(a, rms):
    """One hissing beat: quiet noise with the sound's tone colour, filling the whole beat."""
    noise = lfilter([1.0], a, NOISE.standard_normal(BEAT + 200))[200:]
    return draw_with_chip_steps(noise / (np.sqrt(np.mean(noise ** 2)) + 1e-12) * rms)


def encode(y, s):
    """The recording y as a chip word, in samples."""
    count = max(3, int(round(len(y) / STEP_RATE * s["speed"] / (REPEATS * BEAT / STEP_RATE))))
    moments = listen(y, count)
    loudest = max(m["rms"] for m in moments)
    tone = np.array(s["tone"])
    beats = []
    for m in moments:
        loudness = m["rms"] / loudest
        if loudness < s["gate"]:
            beats += [np.zeros(BEAT, dtype=int)] * REPEATS
        elif m["buzz"] >= s["voiced"] and (loudness >= 0.08 or m["low"] > 0.5):
            # The chip's buzzing sounds are all about as loud as each other, so quiet ones are lifted.
            beat = voiced_beat(m["a"], m["g"], tone, s["v_rms"] * max(loudness, 0.25) ** s["curve"])
            beats += [beat] * REPEATS
        else:
            # The chip's hissing sounds are all about one level loud.
            beats += [hissing_beat(m["a"], s["u_rms"]) for _ in range(REPEATS)]
    samples = np.repeat(np.concatenate(beats), SAMPLES_PER_STEP) / LEVEL_SIZE
    return np.concatenate([np.zeros(LEAD), samples, np.zeros(TAIL)])


def write(path, samples):
    sf.write(path, samples, SAMPLE_RATE, format="OGG", subtype="VORBIS", compression_level=0.0)


# ---------------------------------------------------------------- fitting the settings

def calibrate(work):
    real = [p for p in sorted(BERZERK.glob("*.ogg")) if p.stem not in NOT_FROM_THE_CHIP]
    tones, buzz_rms, hiss_rms, beat_counts = [], [], [], {}
    for p in real:
        beats = [b for b in read_beats(p) if np.abs(b).sum() > 0]
        beat_counts[p.stem] = len(beats)
        for b in beats:
            if is_voiced(b):
                tones.append(tone_of(b))
                buzz_rms.append(np.sqrt(np.mean(b[HALF:] ** 2)))
            else:
                hiss_rms.append(np.sqrt(np.mean(b ** 2)))
    real_tone = np.mean(tones, axis=0)

    recordings = {}
    for w in CALIBRATION_WORDS:
        wav = work / f"{w}.wav"
        say_with_windows(w, wav)
        recordings[w] = read_recording(wav)

    s = {"tone": [1.0] * (HALF + 1), "gate": 0.01, "voiced": 0.5, "curve": 0.25,
         # 1.5 x: with the quiet sounds lifted, this makes the buzzing beats as strong as the chip's.
         "v_rms": 1.5 * float(np.percentile(buzz_rms, 90)), "u_rms": float(np.median(hiss_rms)),
         # How much slower than the David voice the chip's words are spoken.
         "speed": float(np.mean([beat_counts[w] * BEAT / len(recordings[w]) for w in CALIBRATION_WORDS]))}

    for _ in range(4):        # move the tone towards the chip's, a few times over
        ours = []
        for w in CALIBRATION_WORDS:
            tmp = work / "check.ogg"
            write(tmp, encode(recordings[w], s))
            ours += [tone_of(b) for b in read_beats(tmp) if is_voiced(b)]
        change = np.sqrt(real_tone / (np.mean(ours, axis=0) + 1e-12))
        change = np.convolve(change, np.ones(3) / 3, mode="same")
        s["tone"] = list(np.clip(np.array(s["tone"]) * change, 0.05, 50.0))
    SETTINGS.write_text(json.dumps(s, indent=1))
    print(f"wrote {SETTINGS}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("word", nargs="?", help="the word to make, such as family")
    parser.add_argument("--wav", help="your own recording of the word, instead of the Windows voice")
    parser.add_argument("--out", help="where to write it (default: the Berzerk speech folder)")
    parser.add_argument("--force", action="store_true", help="replace a word that is already there")
    parser.add_argument("--calibrate", action="store_true", help="fit the settings again from the real words")
    args = parser.parse_args()

    with tempfile.TemporaryDirectory() as work:
        work = pathlib.Path(work)
        if args.calibrate:
            calibrate(work)
            return 0
        if not args.word:
            parser.error("say which word to make")
        out = pathlib.Path(args.out) if args.out else BERZERK / f"{args.word.lower()}.ogg"
        if out.exists() and not args.force:
            print(f"{out} is already there; use --force to replace it", file=sys.stderr)
            return 1
        wav = pathlib.Path(args.wav) if args.wav else work / "word.wav"
        if not args.wav:
            say_with_windows(args.word, wav)
        samples = encode(read_recording(wav), json.loads(SETTINGS.read_text()))
        write(out, samples)
        print(f"wrote {out} ({len(samples) / SAMPLE_RATE * 1000:.0f} ms)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
