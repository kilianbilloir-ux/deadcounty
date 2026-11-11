#!/usr/bin/env python3
"""Generates the recorded sounds of the game with a local Stable Audio 3 server.

Each entry of SOUNDS becomes Assets/DeadCounty/Audio/Recorded/<Folder>/<Name>_<n>.wav, which the scene builder
uses in place of the cue's synthesised clips (see TestSceneBuilder.Audio.cs). Delete a file to get the
synthesised sound back.

Needs the SA3 Studio server on SA3_STUDIO_URL (default http://127.0.0.1:8420) and numpy + soundfile, so run it
with the server's own interpreter:

    <studio>/stable-audio-3/optimized/mlx/.venv/bin/python scripts/audio/generate_sounds.py [Folder/Name ...]

Seeds are fixed: the same server and weights give the same files again.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.request
from pathlib import Path

import numpy as np
import soundfile as sf

STUDIO_URL = os.environ.get("SA3_STUDIO_URL", "http://127.0.0.1:8420")
STUDIO_LIBRARY = Path(os.environ.get("SA3_STUDIO_LIBRARY", Path.home() / "Documents/ModelOpenSource/Musique/data/library"))
OUT_DIR = Path(__file__).resolve().parents[2] / "Assets/DeadCounty/Audio/Recorded"
AVOID = "music, melody, speech, words, talking, laughter"
# One-shots peak where the synthesised ones do; loops are levelled by loudness, see LOOPS.
PEAK = 0.9
SILENCE = 0.03
MIN_LENGTH = 0.25
RETRIES = 4
SFX = "TrackType: SFX, "

# cue, takes, seconds generated, longest kept (s), prompt.
ONE_SHOTS = [
    ("Zombie/Moan", 5, 3, 2.2, "a single slow guttural zombie moan, a hoarse dead man groaning with a rattling throat, close and dry"),
    ("Zombie/Growl", 4, 2, 1.4, "a short aggressive zombie growl, a snarling wet throat rattle, close and dry"),
    ("Zombie/Alert", 4, 2, 0.9, "a sudden sharp zombie snarl, one startled rasping bark from a dead throat, close and dry"),
    ("Zombie/Breath", 3, 2, 1.6, "one slow wet rattling exhale of a zombie, raspy laboured breathing, close and dry"),
    ("Zombie/Call", 3, 3, 1.8, "a loud hoarse zombie shriek calling the others, an inhuman scream, outdoors"),
    ("Zombie/Frustrated", 3, 3, 1.8, "a frustrated zombie grunting and snarling, angry guttural noises, close and dry"),
    ("Zombie/Sniff", 3, 2, 1.1, "a creature sniffing the air, two short wet sniffs through a rotten nose, close and dry"),
    ("Zombie/Windup", 3, 2, 1.1, "a rising zombie growl building up just before it attacks, close and dry"),
    ("Ambience/Bird", 6, 2, 1.4, "a single small songbird singing one short phrase, outdoors, field recording"),
    ("Ambience/Crow", 2, 3, 2.2, "a crow cawing twice in the distance, open countryside, field recording"),
    ("Ambience/Dog", 2, 3, 2.0, "a dog barking twice far away, rural echo, field recording"),
    ("Ambience/Owl", 2, 3, 2.6, "an owl hooting at night in the distance, quiet forest, field recording"),
    ("Ambience/DistantScream", 2, 3, 2.6, "a human scream of terror very far away, echoing across empty streets"),
    ("Ambience/DistantShot", 2, 3, 2.6, "a single distant gunshot echoing across a valley, long tail"),
    ("Ambience/DistantAlarm", 2, 4, 3.0, "a car alarm wailing far away, muffled by distance, empty town"),
    ("Player/Hurt", 5, 1, 0.5, "a short male grunt of pain, one sharp breathy exclamation, close and dry"),
    ("Player/Death", 2, 3, 2.0, "a man's last choking gasp as he dies, a breathy collapse, close and dry"),
    ("Player/Eat", 2, 2, 1.4, "a person chewing food from a tin can, a few wet bites, close and dry"),
    ("Player/Drink", 2, 2, 1.6, "a person drinking water from a plastic bottle, three gulps, close and dry"),
    ("Player/Bandage", 2, 2, 1.3, "cloth bandage being wrapped and pulled tight around an arm, fabric rustle and tear, close and dry"),
]
# cue, seconds generated, seconds kept, RMS the loop is brought to (that of the synthesised loop it replaces, so
# the cue volumes still hold), model, prompt.
LOOPS = [
    ("Ambience/Wind", 16, 12.0, 0.14, "sm-sfx", SFX + "steady wind blowing across an open field, soft gusts through dry grass, no birds, field recording"),
    ("Ambience/Night", 16, 12.0, 0.1, "sm-sfx", SFX + "crickets chirping in a rural field at night, calm and even, no voices, field recording"),
    ("Music/TitleDrone", 60, 48.0, 0.2, "medium",
     "TrackType: Music, VocalType: Instrumental, Genre: Dark Ambient, a bleak slow drone for the title screen of a zombie survival game "
     "set in a deserted rural county: low bowed strings, a few distant detuned piano notes, a cold wind texture, sparse, melancholic, no drums, 60 BPM"),
    ("Music/PauseTheme", 75, 60.0, 0.12, "medium",
     "TrackType: Music, VocalType: Instrumental, Genre: Ambient, a quiet contemplative piece for a pause menu: a soft felt piano playing "
     "slow sparse notes over a warm low pad, gentle tape hiss, calm and a little sad, no drums, 60 BPM"),
]
LOOP_FADE = 2.0


def generate(prompt: str, model: str, seconds: float, seed: int) -> Path:
    payload = {"prompt": prompt, "dit": model, "seconds": seconds, "steps": 8, "cfg": 1.0, "seed": seed}
    if model == "sm-sfx":
        payload["negative_prompt"] = AVOID
    request = urllib.request.Request(STUDIO_URL + "/api/generate", data=json.dumps(payload).encode(),
                                     headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(request, timeout=900) as response:
        for raw in response:
            if not raw.strip():
                continue
            event = json.loads(raw)
            if event["type"] == "error":
                raise RuntimeError(event["message"])
            if event["type"] == "done":
                return STUDIO_LIBRARY / event["entry"]["file"]
    raise RuntimeError("the server returned nothing")


def fade(samples: np.ndarray, rate: int, attack: float, release: float) -> np.ndarray:
    out = samples.copy()
    a, r = int(attack * rate), int(release * rate)
    shape = (-1,) + (1,) * (out.ndim - 1)
    out[:a] *= np.linspace(0.0, 1.0, a).reshape(shape)
    out[-r:] *= np.linspace(1.0, 0.0, r).reshape(shape)
    return out


def one_shot(path: Path, longest: float) -> tuple[np.ndarray, int] | None:
    """Mono, cut to the sound itself, capped in length; None when the take is silence or a blip."""
    data, rate = sf.read(path, dtype="float32", always_2d=True)
    mono = data.mean(axis=1)
    loud = np.flatnonzero(np.abs(mono) > SILENCE * np.abs(mono).max()) if np.abs(mono).max() > 1e-4 else []
    if len(loud) == 0:
        return None
    start = max(0, loud[0] - int(0.01 * rate))
    mono = mono[start:min(loud[-1] + int(0.05 * rate), start + int(longest * rate))]
    if len(mono) < MIN_LENGTH * rate:
        return None
    mono = fade(mono, rate, 0.004, min(0.12, len(mono) / rate / 4))
    return mono * (PEAK / np.abs(mono).max()), rate


def loop(path: Path, length: float, rms: float, stereo: bool) -> tuple[np.ndarray, int]:
    """`length` seconds whose last LOOP_FADE seconds are folded into the start with equal power, brought to
    `rms`; tanh rounds off the few peaks that the gain pushes past full scale."""
    data, rate = sf.read(path, dtype="float32", always_2d=True)
    if not stereo:
        data = data.mean(axis=1, keepdims=True)
    n, x = int(length * rate), int(LOOP_FADE * rate)
    skip = int(1.0 * rate)
    if len(data) < skip + n + x:
        raise RuntimeError(f"{path.name} is too short for a {length}s loop")
    body = data[skip:skip + n].copy()
    tail = data[skip + n:skip + n + x]
    t = np.linspace(0.0, np.pi / 2, x).reshape(-1, 1)
    body[:x] = body[:x] * np.sin(t) + tail * np.cos(t)
    return np.tanh(body * (rms / float(np.sqrt(np.mean(body ** 2))))), rate


def seed_of(cue: str, take: int, attempt: int) -> int:
    return (sum(ord(c) * (i + 1) for i, c in enumerate(cue)) * 31 + take * 101 + attempt * 7919) % 100000


def main() -> None:
    wanted = set(sys.argv[1:])
    for cue, takes, seconds, longest, prompt in ONE_SHOTS:
        if wanted and cue not in wanted:
            continue
        for take in range(takes):
            for attempt in range(RETRIES):
                result = one_shot(generate(SFX + prompt, "sm-sfx", seconds, seed_of(cue, take, attempt)), longest)
                if result is not None:
                    break
            else:
                raise RuntimeError(f"{cue} take {take}: {RETRIES} silent generations")
            write(cue, take, *result)
    for cue, seconds, length, rms, model, prompt in LOOPS:
        if wanted and cue not in wanted:
            continue
        write(cue, 0, *loop(generate(prompt, model, seconds, seed_of(cue, 0, 0)), length, rms, stereo=cue.startswith("Music/")))


def write(cue: str, take: int, samples: np.ndarray, rate: int) -> None:
    out = OUT_DIR / f"{cue}_{take}.wav"
    out.parent.mkdir(parents=True, exist_ok=True)
    sf.write(out, samples, rate, subtype="PCM_16")
    rms = float(np.sqrt(np.mean(samples ** 2)))
    print(f"{cue}_{take}  {len(samples) / rate:5.2f}s  rms {rms:.3f}", flush=True)


if __name__ == "__main__":
    main()
