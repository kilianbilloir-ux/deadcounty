#!/usr/bin/env python3
"""Generates the recorded sounds of the game with a local Stable Audio 3 server.

Each entry of SOUNDS becomes Assets/DeadCounty/Audio/Recorded/<Folder>/<Name>_<n>.wav, which the scene builder
uses in place of the cue's synthesised clips (see TestSceneBuilder.Audio.cs). Delete a file to get the
synthesised sound back.

Needs the SA3 Studio server on SA3_STUDIO_URL (default http://127.0.0.1:8420) and numpy + soundfile, so run it
with the server's own interpreter:

    <studio>/stable-audio-3/optimized/mlx/.venv/bin/python scripts/audio/generate_sounds.py [--force] [Folder/Name ...]

Only missing takes are generated; --force regenerates the named cues (all of them without names). Seeds are fixed:
the same server and weights give the same files again.
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
# Light blows and a knife's swish are shorter than the blips MIN_LENGTH rejects elsewhere.
MIN_LENGTHS = {"Player/Swing_Blade": 0.12, "Player/Hit_Bat": 0.12, "Player/Hit_Knife": 0.12, "Player/Shove": 0.12}
RETRIES = 4
SFX = "TrackType: SFX, "

# The throats a zombie can have. A voiced cue is recorded in each, takes laid out voice by voice: the scene builder
# marks the cue with as many voices (ZombieVoices in TestSceneBuilder.AudioZombies.cs) and a zombie keeps to one.
VOICES = [
    "in a deep, low, gravelly voice, a big heavy man",
    "in a thin, high, wheezing voice, a frail old man, airy and breathy",
    "in a wet, gurgling, bubbling voice, blood in the throat",
    "in a shrill, raspy, screeching voice, torn vocal cords",
]
# cue, takes per voice, seconds generated, longest kept (s), prompt with {voice}.
VOICED = [
    ("Zombie/Moan", 2, 3, 2.2, "a single slow guttural zombie moan {voice}, a dead man groaning with a rattling throat, close and dry"),
    ("Zombie/Growl", 2, 2, 1.4, "a short aggressive zombie growl {voice}, a snarling throat rattle, close and dry"),
    ("Zombie/Alert", 2, 2, 0.9, "a sudden sharp zombie snarl {voice}, one startled rasping bark from a dead throat, close and dry"),
    ("Zombie/Breath", 2, 2, 1.6, "one slow rattling exhale of a zombie {voice}, laboured breathing, close and dry"),
    ("Zombie/Call", 2, 3, 1.8, "a loud zombie shriek calling the others {voice}, an inhuman scream, outdoors"),
    ("Zombie/Frustrated", 2, 3, 1.8, "a frustrated zombie grunting and snarling {voice}, angry guttural noises, close and dry"),
    ("Zombie/Windup", 2, 2, 1.1, "a rising zombie growl {voice}, building up just before it attacks, close and dry"),
    ("Zombie/Rise", 2, 2, 1.4, "a zombie dragging itself up off the ground {voice}, one long strained groan of effort, close and dry"),
    ("Zombie/Hurt", 2, 1, 0.7, "a zombie struck hard {voice}, one short choked grunt of pain, close and dry"),
    ("Zombie/Death", 2, 3, 1.8, "a zombie's last rattling groan as it dies {voice}, a long exhale fading out, close and dry"),
]
# cue, takes, seconds generated, longest kept (s), prompt.
ONE_SHOTS = [
    ("Zombie/Sniff", 3, 2, 1.1, "a creature sniffing the air, two short wet sniffs through a rotten nose, close and dry"),
    ("Zombie/Fall", 3, 1, 0.8, "a heavy body collapsing onto the ground, one dull thud with a rustle of clothes, close and dry"),
    ("Zombie/Feed", 4, 2, 1.4, "a creature feeding on raw flesh, wet chewing, tearing and slurping, close and dry"),
    ("Ambience/Bird", 6, 2, 1.4, "a single small songbird singing one short phrase, outdoors, field recording"),
    ("Ambience/Crow", 4, 3, 2.2, "a crow cawing twice in the distance, open countryside, field recording"),
    ("Ambience/Woodpecker", 3, 3, 2.0, "a woodpecker drumming on a tree trunk in the distance, one rapid burst of knocks, quiet forest, field recording"),
    ("Ambience/Cow", 3, 3, 2.6, "a cow mooing far away across a field, one long low moo, rural countryside, field recording"),
    ("Ambience/Dog", 4, 3, 2.0, "a dog barking twice far away, rural echo, field recording"),
    ("Ambience/Owl", 4, 3, 2.6, "an owl hooting at night in the distance, quiet forest, field recording"),
    ("Ambience/Coyote", 3, 4, 3.0, "coyotes howling and yipping far away at night, open prairie, field recording"),
    ("Ambience/Shutter", 3, 3, 2.0, "a loose wooden shutter banging twice against a wall in the wind, outdoors, field recording"),
    ("Ambience/DistantScream", 4, 3, 2.6, "a human scream of terror very far away, echoing across empty streets"),
    ("Ambience/DistantShot", 4, 3, 2.6, "a single distant gunshot echoing across a valley, long tail"),
    ("Ambience/DistantAlarm", 3, 4, 3.0, "a car alarm wailing far away, muffled by distance, empty town"),
    ("Ambience/DistantHorde", 3, 4, 3.0, "a crowd of zombies moaning together very far away, a low eerie chorus of groans carried by the wind, empty town"),
    ("Ambience/DistantGlass", 3, 3, 2.2, "a window shattering far away, glass breaking and falling, echoing across empty streets"),
    ("Player/Hurt", 8, 1, 0.5, "a short male grunt of pain, one sharp breathy exclamation, close and dry"),
    ("Player/Death", 3, 3, 2.0, "a man's last choking gasp as he dies, a breathy collapse, close and dry"),
    ("Player/Eat", 4, 2, 1.4, "a person chewing food from a tin can, a few wet bites, close and dry"),
    ("Player/Drink", 4, 2, 1.6, "a person drinking water from a plastic bottle, three gulps, close and dry"),
    ("Player/Bandage", 3, 2, 1.3, "cloth bandage being wrapped and pulled tight around an arm, fabric rustle and tear, close and dry"),
    ("Player/Effort", 6, 1, 0.45, "a man's short grunt of effort as he swings a heavy weapon, one sharp breathy exhale through clenched teeth, close and dry"),
    ("Player/Swing_Blade", 4, 1, 0.4, "a quick airy whoosh of a blade slicing through the air, one short swoosh, close and dry"),
    ("Player/Swing_Heavy", 4, 1, 0.6, "a heavy baseball bat swung hard through the air, one deep powerful whoosh, close and dry"),
    ("Player/Hit_Bat", 4, 1, 0.6, "a heavy punch impact, a wooden club whacking flesh, a meaty body hit, close and dry"),
    ("Player/Hit_Crowbar", 4, 1, 0.6, "melee combat impact, a heavy steel crowbar hitting a body, a meaty thud with a dull metallic ring and a crack of bone, close and dry"),
    ("Player/Hit_Knife", 4, 1, 0.5, "a knife stabbing into flesh, one quick wet stab and squelch, close and dry"),
    ("Player/Shove", 3, 1, 0.5, "two hands shoving a person hard in the chest, a dull body thump and a rustle of clothes, close and dry"),
    ("Player/Stomp", 4, 1, 0.6, "a heavy boot stomping down on a body lying on the ground, a deep thud with a wet crunch, close and dry"),
    ("Player/Kill", 4, 1, 0.6, "a skull cracking under a heavy blow, a sharp crunch of bone and a wet splatter, close and dry"),
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


def one_shot(path: Path, longest: float, shortest: float) -> tuple[np.ndarray, int] | None:
    """Mono, cut to the sound itself, capped in length; None when the take is silence or a blip."""
    data, rate = sf.read(path, dtype="float32", always_2d=True)
    mono = data.mean(axis=1)
    loud = np.flatnonzero(np.abs(mono) > SILENCE * np.abs(mono).max()) if np.abs(mono).max() > 1e-4 else []
    if len(loud) == 0:
        return None
    start = max(0, loud[0] - int(0.01 * rate))
    mono = mono[start:min(loud[-1] + int(0.05 * rate), start + int(longest * rate))]
    if len(mono) < shortest * rate:
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
    force = "--force" in sys.argv
    wanted = set(sys.argv[1:]) - {"--force"}

    def due(cue: str, take: int) -> bool:
        return (not wanted or cue in wanted) and (force or not path_of(cue, take).exists())

    jobs = [(cue, take, seconds, longest, prompt) for cue, takes, seconds, longest, prompt in ONE_SHOTS for take in range(takes)]
    jobs += [(cue, v * per + k, seconds, longest, prompt.format(voice=voice))
             for cue, per, seconds, longest, prompt in VOICED for v, voice in enumerate(VOICES) for k in range(per)]
    for cue, take, seconds, longest, prompt in jobs:
        if not due(cue, take):
            continue
        for attempt in range(RETRIES):
            result = one_shot(generate(SFX + prompt, "sm-sfx", seconds, seed_of(cue, take, attempt)), longest, MIN_LENGTHS.get(cue, MIN_LENGTH))
            if result is not None:
                break
        else:
            raise RuntimeError(f"{cue} take {take}: {RETRIES} silent generations")
        write(cue, take, *result)
    for cue, seconds, length, rms, model, prompt in LOOPS:
        if due(cue, 0):
            write(cue, 0, *loop(generate(prompt, model, seconds, seed_of(cue, 0, 0)), length, rms, stereo=cue.startswith("Music/")))


def path_of(cue: str, take: int) -> Path:
    return OUT_DIR / f"{cue}_{take}.wav"


def write(cue: str, take: int, samples: np.ndarray, rate: int) -> None:
    out = path_of(cue, take)
    out.parent.mkdir(parents=True, exist_ok=True)
    sf.write(out, samples, rate, subtype="PCM_16")
    rms = float(np.sqrt(np.mean(samples ** 2)))
    print(f"{cue}_{take}  {len(samples) / rate:5.2f}s  rms {rms:.3f}", flush=True)


if __name__ == "__main__":
    main()
