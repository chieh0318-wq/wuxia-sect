"""Import the approved Gemini MP3s from a staging directory, or check them with --verify."""

import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
from tempfile import TemporaryDirectory

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / "Assets/Resources/Audio/Music"
TRACKS = {
    "title": "title_b",
    "courtyard": "courtyard_b",
    "jianghu_map_a": "map_a",
    "jianghu_map_b": "map_b",
    "battle_a": "battle_a",
    "battle_b": "battle_b",
    "boss_a": "boss_a",
    "boss_b": "boss_b",
    "sorrow_a": "sorrow_a",
    "sorrow_b": "sorrow_b",
    "victory": "victory_b",
}
RATE = 44100


def ffmpeg(*args):
    executable = os.environ.get("FFMPEG_PATH") or shutil.which("ffmpeg")
    if not executable:
        from imageio_ffmpeg import get_ffmpeg_exe
        executable = get_ffmpeg_exe()
    result = subprocess.run(
        [executable, "-nostdin", "-nostats", *args],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if result.returncode:
        raise RuntimeError(result.stderr.strip())
    return result.stderr


def measure(path):
    log = ffmpeg("-i", path, "-af", "ebur128=peak=true", "-f", "null", "-")
    summary = log.rsplit("Summary:", 1)[-1]
    loudness = float(re.search(r"I:\s+(-?[\d.]+) LUFS", summary)[1])
    peak = float(re.search(r"Peak:\s+(-?[\d.]+) dBFS", summary)[1])
    return loudness, peak


def check(path, victory):
    info = sf.info(path)
    loudness, peak = measure(path)
    duration = info.frames / info.samplerate
    valid = (
        info.format == "OGG" and info.subtype == "VORBIS"
        and info.samplerate == RATE and info.channels == 2
        and abs(loudness + 16) <= 1 and peak <= -1
        and (not victory or duration <= 5)
    )
    return valid, f"{duration:.3f} s | {loudness:.1f} LUFS | {peak:.1f} dBFS"


def prepare(samples, victory):
    audible = np.flatnonzero(np.max(np.abs(samples), axis=1) > 10 ** (-50 / 20))
    if not len(audible):
        raise ValueError("No samples above the silence threshold")
    audio = samples[audible[0]:audible[-1] + 1].copy()
    if victory:
        audio = audio[:int(4.85 * RATE)]
        fade = min(len(audio), int(.3 * RATE))
        audio[-fade:] *= np.linspace(1, 0, fade)[:, None]
        return audio

    frames = 3 * RATE
    if len(audio) < 2 * frames:
        raise ValueError("Loop needs at least six seconds of audio")
    phase = np.linspace(0, np.pi / 2, frames)[:, None]
    loop = audio[:-frames].copy()
    loop[:frames] = audio[-frames:] * np.cos(phase) + audio[:frames] * np.sin(phase)
    return loop


def convert(source, destination, victory):
    if destination.exists():
        valid, measurements = check(destination, victory)
        if not valid:
            raise ValueError(f"Review existing output before replacing it: {measurements}")
        return

    with TemporaryDirectory(dir=destination.parent) as folder:
        wave = Path(folder) / "working.wav"
        encoded = Path(folder) / destination.name
        ffmpeg("-v", "error", "-i", source, "-ar", str(RATE), "-ac", "2",
               "-c:a", "pcm_f32le", wave)
        samples, _ = sf.read(wave)
        sf.write(wave, prepare(samples, victory), RATE, subtype="FLOAT")
        loudness, peak = measure(wave)
        gain = -16 - loudness
        ceiling = -1.5
        for _ in range(8):
            filters = f"volume={gain}dB"
            if peak + gain > ceiling:
                filters += f",alimiter=limit={10 ** (ceiling / 20)}:level=false:latency=true"
            ffmpeg("-y", "-v", "error", "-i", wave, "-af", filters,
                   "-c:a", "libvorbis", "-q:a", "6", encoded)
            valid, measurements = check(encoded, victory)
            if valid:
                os.link(encoded, destination)
                return
            loudness, encoded_peak = measure(encoded)
            gain += -16 - loudness
            if encoded_peak > -1:
                ceiling -= encoded_peak + 1.3
        raise ValueError(f"Could not meet output targets: {measurements}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("staging", nargs="?", type=Path)
    mode.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    failed = False
    for track, original in TRACKS.items():
        path = AUDIO / f"{track}_g1.ogg"
        victory = track == "victory"
        try:
            if not args.verify:
                convert(args.staging / f"{original}.mp3", path, victory)
            valid, details = check(path, victory)
        except (OSError, RuntimeError, ValueError) as error:
            valid, details = False, str(error)
        print(f"{'OK' if valid else 'FAIL'} | {path.name} | {details}", flush=True)
        failed |= not valid
    return int(failed)


if __name__ == "__main__":
    raise SystemExit(main())
