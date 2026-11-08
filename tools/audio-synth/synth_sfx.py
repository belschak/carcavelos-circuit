"""Generates every original sound of Carcavelos Circuit (Role 4, task R5).

Pure Python (no extra packages): sine/saw waves, filtered noise and envelopes,
written as 16-bit mono WAV files. Run it again to rebuild the sounds exactly:

    python tools/audio-synth/synth_sfx.py Assets/_Project/Audio/Clips

All sounds are our own work (no samples), so they are free to use (CC0).
"""

import math
import random
import sys
import wave
from array import array
from pathlib import Path

SR = 44100  # samples per second
TAU = 2.0 * math.pi


# ---------- small building blocks ----------

def silence(seconds):
    return [0.0] * int(seconds * SR)


def note_freq(name):
    """'A4' -> 440.0 Hz. Sharps as 'C#5'."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3,
             "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    semitones = names[pitch] + (octave - 4) * 12
    return 440.0 * 2.0 ** (semitones / 12.0)


def envelope(n, attack, release, curve=4.0):
    """Linear attack, exponential-ish release over the last `release` seconds."""
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    out = []
    for i in range(n):
        g = 1.0
        if i < a:
            g = i / a
        tail = n - i
        if tail < r:
            g *= (tail / r) ** (curve / 2.0)
        out.append(g)
    return out


def exp_decay(n, seconds_to_quiet):
    k = 6.9 / (seconds_to_quiet * SR)  # -60 dB after seconds_to_quiet
    return [math.exp(-k * i) for i in range(n)]


def tone(freq, seconds, partials=((1, 1.0),), vibrato=0.0, vib_rate=5.5, glide_to=None):
    """Sum of sine partials; optional vibrato (in semitones) and pitch glide."""
    n = int(seconds * SR)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = freq
        if glide_to is not None:
            f = freq * (glide_to / freq) ** (i / n)
        if vibrato:
            f *= 2.0 ** (vibrato * math.sin(TAU * vib_rate * t) / 12.0)
        phase += TAU * f / SR
        s = 0.0
        for mult, amp in partials:
            s += amp * math.sin(phase * mult)
        out.append(s)
    return out


def noise(n, seed):
    rnd = random.Random(seed)
    return [rnd.uniform(-1.0, 1.0) for _ in range(n)]


def lowpass(x, cutoff):
    """One-pole low-pass. cutoff may be a number or a list (sweep)."""
    out = []
    y = 0.0
    for i, s in enumerate(x):
        c = cutoff[i] if isinstance(cutoff, list) else cutoff
        a = 1.0 - math.exp(-TAU * c / SR)
        y += a * (s - y)
        out.append(y)
    return out


def highpass(x, cutoff):
    low = lowpass(x, cutoff)
    return [s - l for s, l in zip(x, low)]


def bandpass(x, center, q=4.0):
    """State-variable band-pass; center may be a list for sweeps."""
    out = []
    low = band = 0.0
    damp = 1.0 / q
    for i, s in enumerate(x):
        c = center[i] if isinstance(center, list) else center
        f = 2.0 * math.sin(math.pi * min(c, SR / 6.0) / SR)
        high = s - low - damp * band
        band += f * high
        low += f * band
        out.append(band)
    return out


def mul(a, b):
    return [x * y for x, y in zip(a, b)]


def scale(x, g):
    return [s * g for s in x]


def mix_into(target, source, start_seconds, gain=1.0):
    start = int(start_seconds * SR)
    need = start + len(source)
    if need > len(target):
        target.extend([0.0] * (need - len(target)))
    for i, s in enumerate(source):
        target[start + i] += s * gain
    return target


def normalise(x, peak_db=-1.0):
    peak = max(abs(s) for s in x) or 1.0
    g = 10.0 ** (peak_db / 20.0) / peak
    return [s * g for s in x]


def soft_clip(x, drive=1.0):
    return [math.tanh(s * drive) for s in x]


def loop_crossfade(x, fade_seconds):
    """Make a seamless loop: the extra tail is faded over the head, then cut."""
    f = int(fade_seconds * SR)
    body = x[:-f]
    tail = x[-f:]
    for i in range(f):
        w = i / f
        body[i] = body[i] * w + tail[i] * (1.0 - w)
    return body


def write_wav(path, samples):
    data = array("h", (int(max(-1.0, min(1.0, s)) * 32767) for s in samples))
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"{path.name}: {len(samples) / SR:.2f} s")


# ---------- race interface sounds ----------

def countdown_beep():
    # 3, 2, 1: short 440 Hz beep with a little edge so it cuts through the engine.
    x = tone(440.0, 0.15, partials=((1, 1.0), (3, 0.25), (5, 0.08)))
    return normalise(mul(x, envelope(len(x), 0.004, 0.05)), -3.0)


def go_beep():
    # GO: one octave higher and longer, with a fifth on top.
    x = tone(880.0, 0.6, partials=((1, 1.0), (1.5, 0.35), (3, 0.15)))
    return normalise(mul(x, envelope(len(x), 0.004, 0.35)), -2.0)


def bell(freq, seconds, bright=1.0):
    partials = ((1, 1.0), (2.0, 0.5 * bright), (2.76, 0.35 * bright), (5.4, 0.12 * bright))
    x = tone(freq, seconds, partials=partials)
    return mul(x, mul(exp_decay(len(x), seconds), envelope(len(x), 0.002, 0.02)))


def checkpoint_chime():
    out = []
    mix_into(out, bell(note_freq("E6"), 0.5), 0.0, 0.7)
    mix_into(out, bell(note_freq("B6"), 0.6), 0.07, 0.6)
    return normalise(out, -4.0)


def lead_voice(freq, seconds, vibrato=0.08):
    """Warm square-ish synth voice for the jingles."""
    partials = ((1, 1.0), (2, 0.18), (3, 0.33), (5, 0.18), (7, 0.1))
    x = tone(freq, seconds, partials=partials, vibrato=vibrato)
    return lowpass(mul(x, envelope(len(x), 0.01, min(0.25, seconds * 0.6))), 3500.0)


def finish_jingle():
    # Original 2-second fanfare: quick rising arpeggio, then a held major chord.
    out = []
    steps = ["G4", "C5", "E5", "G5"]
    for i, name in enumerate(steps):
        mix_into(out, lead_voice(note_freq(name), 0.16, vibrato=0.0), i * 0.11, 0.55)
    for name in ["C5", "E5", "G5", "C6"]:
        mix_into(out, lead_voice(note_freq(name), 1.3), 0.46, 0.32)
    mix_into(out, bell(note_freq("C7"), 1.0, bright=0.6), 0.46, 0.25)
    return normalise(soft_clip(out, 1.2), -2.0)


def new_record_sting():
    # Sparkly bells going up: plays together with the finish jingle.
    out = []
    for i, name in enumerate(["C6", "E6", "G6", "C7", "E7"]):
        mix_into(out, bell(note_freq(name), 0.7, bright=1.2), 0.5 + i * 0.07, 0.5)
    return normalise(out, -4.0)


# ---------- kart sounds ----------

def drift_ping(stage):
    # Stage 1 (blue): one rising ping. Stage 2 (orange): brighter double ping.
    start, end = (900.0, 1500.0) if stage == 1 else (1300.0, 2300.0)
    out = []
    count = 1 if stage == 1 else 2
    for i in range(count):
        x = tone(start, 0.2, partials=((1, 1.0), (2, 0.3), (3, 0.15)), glide_to=end)
        x = mul(x, envelope(len(x), 0.003, 0.15))
        mix_into(out, x, i * 0.09, 0.8)
    return normalise(out, -4.0)


def boost_whoosh():
    # Air rushing past: noise through a band-pass that sweeps up and down, plus a low push.
    seconds = 0.9
    n = int(seconds * SR)
    centers = []
    for i in range(n):
        t = i / n
        centers.append(300.0 + 3200.0 * math.sin(math.pi * min(1.0, t * 1.3)) ** 2)
    air = bandpass(noise(n, 7), centers, q=1.6)
    air = mul(air, envelope(n, 0.05, 0.55))
    push = tone(140.0, seconds, glide_to=55.0)
    push = mul(push, exp_decay(n, 0.5))
    out = [a * 1.0 + p * 0.5 for a, p in zip(air, push)]
    return normalise(soft_clip(out, 1.5), -2.0)


def landing_thud():
    seconds = 0.35
    n = int(seconds * SR)
    body = tone(110.0, seconds, glide_to=42.0)
    body = mul(body, exp_decay(n, 0.3))
    crunch = lowpass(noise(n, 11), 900.0)
    crunch = mul(crunch, exp_decay(n, 0.12))
    out = [b + c * 2.5 for b, c in zip(body, crunch)]
    out = mul(out, envelope(n, 0.002, 0.05))
    return normalise(soft_clip(out, 1.6), -2.0)


def engine_loop():
    # Two-stroke kart engine at idle-to-mid revs. Base firing rate 110 Hz; Unity's pitch (0.8 to 1.8)
    # raises it to about 200 Hz at top speed. 2 s = exactly 220 cycles, so harmonics loop without a click.
    seconds = 2.0
    fade = 0.15
    n = int((seconds + fade) * SR)
    f0 = 110.0
    rnd = random.Random(3)
    raw_noise = [rnd.uniform(-1.0, 1.0) for _ in range(n)]
    out = []
    for i in range(n):
        t = i / SR
        phase = TAU * f0 * t
        # Slight slow wobble (exactly 1 cycle per loop) so it does not sound like a test tone.
        wobble = 1.0 + 0.04 * math.sin(TAU * 0.5 * t)
        s = 0.0
        for h in range(1, 12):
            s += math.sin(phase * h + h * 0.7) / (h ** 0.9)
        # Each firing is a short pressure pulse: amplitude follows the firing phase.
        pulse = 0.6 + 0.4 * max(0.0, math.cos(phase)) ** 3
        # Exhaust rasp: noise gated by the same firing pulses.
        rasp = raw_noise[i] * (max(0.0, math.cos(phase)) ** 6)
        out.append((s * pulse * 0.35 + rasp * 0.5) * wobble)
    out = lowpass(out, 2600.0)
    out = highpass(out, 60.0)
    out = soft_clip(out, 2.2)
    out = loop_crossfade(out, fade)
    return normalise(out, -3.0)


def drift_loop():
    # Tyre screech: resonant noise around 2 kHz plus a few squealing tones that wander a little.
    seconds = 1.5
    fade = 0.2
    n = int((seconds + fade) * SR)
    hiss = bandpass(noise(n, 5), 2300.0, q=6.0)
    squeal = []
    phases = [0.0, 0.0, 0.0]
    bases = [1820.0, 2140.0, 2610.0]
    for i in range(n):
        t = i / SR
        s = 0.0
        for k in range(3):
            f = bases[k] * (1.0 + 0.012 * math.sin(TAU * (0.67 + k * 0.4) * t + k))
            phases[k] += TAU * f / SR
            s += math.sin(phases[k]) * (0.5 + 0.5 * math.sin(TAU * (1.33 + k) * t)) ** 2
        squeal.append(s)
    out = [h * 3.0 + q * 0.35 for h, q in zip(hiss, squeal)]
    out = soft_clip(out, 1.4)
    out = loop_crossfade(out, fade)
    return normalise(out, -4.0)


def ui_click():
    # Soft, short interface tick: a quick high blip with a tiny bit of noise for the "tap".
    seconds = 0.1
    n = int(seconds * SR)
    blip = tone(1500.0, seconds, partials=((1, 1.0), (2, 0.25)), glide_to=1100.0)
    blip = mul(blip, exp_decay(n, 0.035))
    tap = highpass(noise(n, 21), 2500.0)
    tap = mul(tap, exp_decay(n, 0.008))
    out = [b + t * 0.4 for b, t in zip(blip, tap)]
    out = mul(out, envelope(n, 0.001, 0.02))
    return normalise(out, -6.0)


def respawn_whoosh():
    # Airy whoosh that rises, plus a soft sparkle on top: "you are back on the track".
    seconds = 0.6
    n = int(seconds * SR)
    centers = [600.0 + 4000.0 * (i / n) ** 1.5 for i in range(n)]
    air = bandpass(noise(n, 31), centers, q=2.0)
    air = mul(air, envelope(n, 0.12, 0.3))
    sparkle = [0.0] * n
    for k, (start, freq) in enumerate(((0.18, 1760.0), (0.26, 2217.0), (0.34, 2637.0))):
        note = mul(tone(freq, 0.25, partials=((1, 1.0), (3, 0.2))), exp_decay(int(0.25 * SR), 0.08))
        mix_into(sparkle, note, start, 0.25)
    out = [a * 1.4 + s for a, s in zip(air, sparkle)]
    return normalise(soft_clip(out, 1.3), -4.0)


def wind_loop():
    # Steady wind rush for speed feel. KartAudio raises its volume and pitch with speed.
    seconds = 3.0
    fade = 0.4
    n = int((seconds + fade) * SR)
    # A slow sweep (exactly one cycle per loop) so it breathes instead of sounding like static.
    centers = [700.0 + 250.0 * math.sin(TAU * (i / SR) / seconds) for i in range(n)]
    rush = bandpass(noise(n, 41), centers, q=0.8)
    low = lowpass(noise(n, 43), 250.0)
    out = [r + l * 2.0 for r, l in zip(rush, low)]
    out = loop_crossfade(out, fade)
    return normalise(out, -6.0)


# ---------- music ----------

def music_loop():
    """Original 32-second beach synth-pop loop (120 BPM, A minor: Am - F - C - G).

    Bars 1-8: drums, bass and pads. Bars 9-16: the lead melody joins.
    Length is exactly 16 bars, so it loops on the beat.
    """
    bpm = 120.0
    beat = 60.0 / bpm
    bar = 4 * beat
    bars = 16
    total = bars * bar
    out = [0.0] * int(total * SR)

    chords = [["A3", "C4", "E4"], ["F3", "A3", "C4"], ["C4", "E4", "G4"], ["G3", "B3", "D4"]]
    roots = ["A2", "F2", "C3", "G2"]

    # Drums: kick on 1 and 3, snare on 2 and 4, off-beat hi-hats.
    kick_n = int(0.3 * SR)
    kick = mul(tone(150.0, 0.3, glide_to=45.0), exp_decay(kick_n, 0.28))
    snare_n = int(0.2 * SR)
    snare = [a * 0.6 + b for a, b in zip(
        mul(tone(190.0, 0.2), exp_decay(snare_n, 0.08)),
        mul(highpass(noise(snare_n, 21), 1500.0), exp_decay(snare_n, 0.15)))]
    hat_n = int(0.05 * SR)
    hat = mul(highpass(noise(hat_n, 31), 7000.0), exp_decay(hat_n, 0.04))

    for b in range(bars):
        t0 = b * bar
        for k in range(4):
            if k in (0, 2):
                mix_into(out, kick, t0 + k * beat, 0.9)
            else:
                mix_into(out, snare, t0 + k * beat, 0.45)
            mix_into(out, hat, t0 + k * beat + beat / 2, 0.25)
            if b >= 8:
                mix_into(out, hat, t0 + k * beat + beat / 4 * 3, 0.12)

        chord = chords[b % 4]
        root = roots[b % 4]
        # Bass: eighth notes, plucky saw-like tone.
        for e in range(8):
            f = note_freq(root) * (2.0 if e in (3, 7) else 1.0)
            n = int(beat / 2 * SR)
            x = tone(f, beat / 2, partials=((1, 1.0), (2, 0.5), (3, 0.33), (4, 0.25)))
            x = lowpass(mul(x, envelope(n, 0.005, 0.12)), 700.0)
            mix_into(out, x, t0 + e * beat / 2, 0.32)
        # Pad: soft chord held over the bar.
        for name in chord:
            x = tone(note_freq(name), bar, partials=((1, 1.0), (2, 0.2), (3, 0.08)), vibrato=0.05, vib_rate=4.0)
            x = mul(x, envelope(len(x), 0.25, 0.4))
            mix_into(out, x, t0, 0.07)

    # Lead melody (our own), bars 9-16. (note, start beat in bar, length in beats)
    phrase_a = [("E5", 0, 1), ("A5", 1, 0.5), ("G5", 1.5, 0.5), ("E5", 2, 1.5)]
    phrase_b = [("C5", 0, 0.5), ("D5", 0.5, 0.5), ("E5", 1, 1), ("C5", 2, 1.5)]
    phrase_c = [("G5", 0, 1), ("E5", 1, 0.5), ("D5", 1.5, 0.5), ("C5", 2, 1), ("D5", 3, 1)]
    phrase_d = [("B4", 0, 0.5), ("D5", 0.5, 0.5), ("G5", 1, 1), ("D5", 2, 2)]
    melody = [phrase_a, phrase_b, phrase_c, phrase_d, phrase_a, phrase_b, phrase_c,
              [("B4", 0, 0.5), ("D5", 0.5, 0.5), ("E5", 1, 3)]]
    for i, phrase in enumerate(melody):
        t0 = (8 + i) * bar
        for name, start, length in phrase:
            mix_into(out, lead_voice(note_freq(name), length * beat * 0.95), t0 + start * beat, 0.16)

    # The tail of the last notes would spill past the loop end: fold it back to the start.
    n_loop = int(total * SR)
    for i in range(n_loop, len(out)):
        out[i - n_loop] += out[i]
    out = out[:n_loop]
    return normalise(soft_clip(out, 1.1), -3.0)


SOUNDS = {
    "sfx_countdown_beep": countdown_beep,
    "sfx_go_beep": go_beep,
    "sfx_checkpoint_chime": checkpoint_chime,
    "sfx_finish_jingle": finish_jingle,
    "sfx_new_record": new_record_sting,
    "sfx_drift_stage1": lambda: drift_ping(1),
    "sfx_drift_stage2": lambda: drift_ping(2),
    "sfx_boost_whoosh": boost_whoosh,
    "sfx_landing_thud": landing_thud,
    "loop_engine": engine_loop,
    "loop_drift": drift_loop,
    "music_campus_loop": music_loop,
    "ui_click": ui_click,
    "sfx_respawn": respawn_whoosh,
    "amb_wind_loop": wind_loop,
}


def main():
    target = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    target.mkdir(parents=True, exist_ok=True)
    only = sys.argv[2:]  # optional: names to rebuild
    for name, make in SOUNDS.items():
        if only and name not in only:
            continue
        write_wav(target / f"{name}.wav", make())


if __name__ == "__main__":
    main()
