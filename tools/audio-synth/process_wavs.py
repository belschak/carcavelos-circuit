"""Mastering step for the game's WAV files (Role 4, task P4 audio polish).

Run it after synth_sfx.py (and after Luc delivers new kart sounds). It does three things per file:

1. Re-level by loudness (RMS), not by peak, to the target in the table below, so a beep and an engine loop
   that "look" equally loud also sound equally loud. A peak never goes above -1 dBFS (it lowers the gain
   instead, and prints that).
2. Loops only: 2048 samples of silence at the start. Unity's Web build re-encodes every clip to AAC with the
   Windows codec, which changes the first 1024 samples; the loop therefore starts after the silence.
3. Loops only: a 'smpl' chunk with loop start = 2048 and loop end = last sample. Unity reads these loop points.

Safe to run again: a file that already has our loop chunk is un-padded first, so it never gets padded twice.
Only the files in the table are touched (our own synth sounds and Luc's ElevenLabs kart sounds).

    python -I tools/audio-synth/process_wavs.py Assets/_Project/Audio/Clips            (writes the files)
    python -I tools/audio-synth/process_wavs.py Assets/_Project/Audio/Clips --dry-run  (only prints)
    python -I tools/audio-synth/process_wavs.py Assets/_Project/Audio/Clips --backup <folder>  (copy originals first)

Standard library only (wave, struct, array), no numpy.
"""

import math
import shutil
import struct
import sys
import wave
from array import array
from pathlib import Path

LOOP_PAD = 2048          # frames of silence before a loop starts
PEAK_CEILING_DB = -1.0   # no sample may be louder than this

# name: (RMS target in dBFS or None = keep the level, is it a loop?, soft-limit the peaks?)
# Targets from audio-polish-plan.md section 2.3. Engine and squeal keep Luc's levels.
# Soft limit = only for our own bell sounds: their first click is so much louder than the ring that the
# -1 dBFS ceiling would leave them 3-4 dB too quiet. A gentle tanh curve rounds off just those peaks.
PLAN = {
    # engine bus (anchor)
    "kart_engine_low": (None, True, False),
    "kart_engine_high": (None, True, False),
    # kart effects bus
    "kart_drift_squeal": (None, True, False),
    "kart_drift_sparks": (-21.0, True, False),
    "amb_wind_loop": (-22.0, True, False),
    "kart_drift_start": (-15.0, False, False),
    "kart_drift_stage_up": (-15.0, False, False),
    "el_boost_whoosh": (-15.0, False, False),
    "sfx_landing_thud": (-15.0, False, False),
    "sfx_respawn": (-15.0, False, False),
    "sfx_shift_pop": (-15.0, False, False),
    "sfx_wall_bump": (-15.0, False, False),
    "sfx_takeoff_whoosh": (-17.0, False, False),
    # stingers bus (played at volume 1.0, the level lives in the file)
    "sfx_countdown_beep": (-15.0, False, False),
    "sfx_go_beep": (-13.0, False, False),
    "sfx_checkpoint_chime": (-16.0, False, True),
    "sfx_finish_jingle": (-12.0, False, False),
    "sfx_new_record": (-14.0, False, True),
    "sfx_hint_warning": (-15.0, False, False),
    # music
    "music_campus_loop": (-16.0, True, False),
    "music_results_loop": (-16.0, True, False),
    # interface
    "ui_click": (-20.0, False, False),
    "ui_open": (-21.0, False, False),
    "ui_close": (-21.0, False, False),
    "ui_page": (-23.0, False, False),
}


# ---------- reading and writing ----------

def read_chunks(path):
    """All RIFF chunks of a WAV file as a dict id -> bytes (first of each id)."""
    data = path.read_bytes()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError(f"{path.name}: not a WAV file")
    chunks = {}
    pos = 12
    while pos + 8 <= len(data):
        cid = data[pos:pos + 4]
        size = struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = data[pos + 8:pos + 8 + size]
        if cid not in chunks:
            chunks[cid] = body
        pos += 8 + size + (size & 1)  # chunks are padded to an even length
    return chunks


def read_wav(path):
    """Returns (samples as floats -1..1 interleaved, channels, sample rate, loop start or None)."""
    with wave.open(str(path), "rb") as w:
        channels = w.getnchannels()
        rate = w.getframerate()
        if w.getsampwidth() != 2:
            raise ValueError(f"{path.name}: only 16-bit WAV is supported")
        raw = w.readframes(w.getnframes())
    ints = array("h")
    ints.frombytes(raw)
    if sys.byteorder == "big":
        ints.byteswap()
    samples = [s / 32768.0 for s in ints]

    loop_start = None
    smpl = read_chunks(path).get(b"smpl")
    if smpl is not None and len(smpl) >= 36 + 24:
        loop_count = struct.unpack("<I", smpl[28:32])[0]
        if loop_count > 0:
            loop_start = struct.unpack("<I", smpl[36 + 8:36 + 12])[0]
    return samples, channels, rate, loop_start


def write_wav(path, samples, channels, rate, loop_start=None):
    """16-bit PCM WAV. With loop_start: adds a 'smpl' chunk that loops loop_start .. last frame."""
    # Same scale as read_wav (32768), so reading and writing an unchanged file gives the same bytes.
    ints = array("h", (max(-32768, min(32767, int(round(s * 32768.0)))) for s in samples))
    if sys.byteorder == "big":
        ints.byteswap()
    pcm = ints.tobytes()
    frames = len(samples) // channels

    fmt = struct.pack("<HHIIHH", 1, channels, rate, rate * channels * 2, channels * 2, 16)
    body = b"WAVE"
    body += b"fmt " + struct.pack("<I", len(fmt)) + fmt
    body += b"data" + struct.pack("<I", len(pcm)) + pcm
    if len(pcm) & 1:
        body += b"\0"
    if loop_start is not None:
        # smpl header: manufacturer, product, sample period (ns), MIDI note 60, pitch fraction,
        # SMPTE format, SMPTE offset, 1 loop, 0 extra bytes. Then one loop: id, type 0 (forward), start, end,
        # fraction, play count 0 (= forever).
        header = struct.pack("<IIIIIIIII", 0, 0, int(1e9 / rate), 60, 0, 0, 0, 1, 0)
        loop = struct.pack("<IIIIII", 0, 0, loop_start, frames - 1, 0, 0)
        smpl = header + loop
        body += b"smpl" + struct.pack("<I", len(smpl)) + smpl
    path.write_bytes(b"RIFF" + struct.pack("<I", len(body)) + body)


# ---------- levels ----------

def db(value):
    return 20.0 * math.log10(value) if value > 0 else -120.0


def rms_db(samples):
    if not samples:
        return -120.0
    return db(math.sqrt(sum(s * s for s in samples) / len(samples)))


def peak_db(samples):
    return db(max((abs(s) for s in samples), default=0.0))


def gain_for(samples, target_rms_db):
    """Gain in dB that brings the RMS to the target, lowered if a peak would pass the ceiling."""
    want = target_rms_db - rms_db(samples)
    headroom = PEAK_CEILING_DB - peak_db(samples)
    return min(want, headroom), want > headroom


def soft_limit(samples, target_rms_db):
    """Raise to the RMS target and round off peaks above the ceiling with tanh (at most 4 dB of extra gain).
    tanh is almost a straight line for quiet samples, so only the loud first click of a bell changes."""
    ceiling = 10.0 ** (PEAK_CEILING_DB / 20.0)
    gain_db = min(target_rms_db - rms_db(samples), PEAK_CEILING_DB - peak_db(samples) + 4.0)
    out = samples
    for _ in range(6):  # tanh lowers the RMS a little, so correct the gain a few times
        g = 10.0 ** (gain_db / 20.0)
        out = [ceiling * math.tanh(s * g / ceiling) for s in samples]
        gain_db += target_rms_db - rms_db(out)
        gain_db = min(gain_db, PEAK_CEILING_DB - peak_db(samples) + 4.0)
    return out, gain_db


# ---------- main ----------

def process(path, target, is_loop, use_soft_limit, dry_run):
    samples, channels, rate, loop_start = read_wav(path)

    # Already processed by us: strip the old silence so it is not padded twice.
    if loop_start == LOOP_PAD and len(samples) > LOOP_PAD * channels:
        samples = samples[LOOP_PAD * channels:]

    before_rms, before_peak = rms_db(samples), peak_db(samples)
    gain_db, limited = (0.0, False)
    # A soft-limited file that is already within 1.5 dB of its target was mastered before: limiting it again
    # would round the peaks off a second time, so it is left alone (a second run changes nothing).
    if target is not None and use_soft_limit and abs(target - before_rms) <= 1.5:
        use_soft_limit = False
        target = None
    if target is not None and use_soft_limit:
        samples, gain_db = soft_limit(samples, target)
        limited = rms_db(samples) < target - 0.5
    else:
        if target is not None:
            gain_db, limited = gain_for(samples, target)
            # Already close (Luc's kart files, a second run): skip changes under 0.05 dB.
            if abs(gain_db) < 0.05:
                gain_db = 0.0
        g = 10.0 ** (gain_db / 20.0)
        samples = [s * g for s in samples]

    new_loop_start = None
    if is_loop:
        samples = [0.0] * (LOOP_PAD * channels) + samples
        new_loop_start = LOOP_PAD

    body = samples[LOOP_PAD * channels:] if is_loop else samples
    note = " (peak-limited, below target)" if limited else (" (soft-limited peaks)" if use_soft_limit else "")
    print(f"{path.name:28s} rms {before_rms:6.1f} -> {rms_db(body):6.1f} dBFS, "
          f"peak {before_peak:6.1f} -> {peak_db(body):6.1f}, gain {gain_db:+5.1f} dB"
          f"{', loop pad + smpl' if is_loop else ''}{note}")
    if not dry_run:
        write_wav(path, samples, channels, rate, new_loop_start)


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    folder = Path(args[0])
    dry_run = "--dry-run" in args
    backup = None
    if "--backup" in args:
        backup = Path(args[args.index("--backup") + 1])
        backup.mkdir(parents=True, exist_ok=True)

    for name, (target, is_loop, use_soft_limit) in PLAN.items():
        path = folder / f"{name}.wav"
        if not path.exists():
            print(f"{path.name:28s} missing, skipped")
            continue
        if backup is not None and not dry_run:
            copy = backup / path.name
            if not copy.exists():  # keep the very first original, never overwrite a backup
                shutil.copy2(path, copy)
        process(path, target, is_loop, use_soft_limit, dry_run)


if __name__ == "__main__":
    main()
