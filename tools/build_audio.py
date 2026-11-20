"""Render original plucked-string loops and interface cues without dependencies."""
from array import array
import math
from pathlib import Path
import random
import wave

ROOT = Path(__file__).resolve().parents[1] / "Assets/Audio"
RATE = 22050
rng = random.Random(732)
scale = [0, 2, 4, 7, 9, 12, 14, 16, 19]

def write(group, name, signal):
    path = ROOT / group / (name + ".wav")
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as file:
        file.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        file.writeframes(array("h", (int(max(-1, min(1, x)) * 30000) for x in signal)).tobytes())

def pluck(signal, start, midi, volume, seconds, loop=False):
    delay = round(RATE / (440 * 2 ** ((midi - 69) / 12)))
    string = [rng.uniform(-1, 1) for _ in range(delay)]
    for i in range(int(seconds * RATE)):
        index = i % delay
        sample = string[index]
        string[index] = (sample * .55 + string[(index + 1) % delay] * .45) * .998
        for tap, gain in [(0, 1), (1733, .22), (4693, .11), (7673, .06)]:
            target = start + i + tap
            if loop or target < len(signal):
                signal[target % len(signal)] += sample * volume * gain * min(i / 75, 1)

for name, root, pulse, notes in [
    ("courtyard", 50, 1.25, [0,4,3,2,5,4,2,1,0,3,4,7,6,4,2,1]),
    ("map", 48, 1.05, [0,3,5,4,6,7,5,3,2,4,3,5,8,7,4,1]),
    ("prologue", 45, 1.5, [3,2,0,1,2,0,3,2,5,4,2,1,3,2,1,0]),
]:
    signal = [0.] * round(pulse * 16 * RATE)
    for i, note in enumerate(notes):
        start = round(i * pulse * RATE)
        pluck(signal, start, root + scale[note], .32, 3.5, True)
        if i % 4 == 0:
            pluck(signal, start, root - 12, .21, 4, True)
    write("Music", name, signal)

for name, length, notes, noise in [
    ("click", .12, [83], .1), ("hover", .085, [91], .03),
    ("place", .48, [40, 37], .4), ("complete", .78, [69, 76, 81], 0),
    ("page", .3, [], .28), ("month", 1.6, [37, 49], 0),
    ("recruit", .56, [64, 71, 76], 0), ("departure", .5, [52, 59], .07),
    ("arrival", .8, [55, 62, 67], 0), ("coin", .25, [90, 95], 0),
]:
    count = int(length * RATE)
    signal = [rng.uniform(-1,1) * noise * math.sin(math.pi * i / count) * math.exp(-5*i/count)
              for i in range(count)]
    for i, note in enumerate(notes):
        pluck(signal, i * 1850, note, .44, length * .8)
    write("Sfx", name, [value * min(1, (count-i)/500) for i, value in enumerate(signal)])
