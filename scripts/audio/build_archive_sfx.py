#!/usr/bin/env python3
"""Edit licensed human-authored foley into the game's SFX. Requires ffmpeg and soundfile; no synthesis.

Downloads are cached outside Assets, verified by SHA256, and never executed.
The checked-in manifest records the exact source member and processing for each cue.
"""
import argparse
import array
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import urllib.request
import wave
import zipfile
import soundfile

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Game/Assets/_Project/Resources/Audio/SFX'
SOURCES = {
    'rpg': ('rpg-audio.zip', 'https://opengameart.org/sites/default/files/RPGsounds_Kenney.zip', '3ae398ad63e293f9c450bda22d5d81c3af69c74df66fc1400f33c012c0bbc231', 'Kenney', 'https://opengameart.org/content/50-rpg-sound-effects'),
    'interface': ('interface-sounds.zip', 'https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip', 'f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232', 'Kenney', 'https://opengameart.org/content/interface-sounds'),
    'owlish': ('owlish.zip', 'https://opengameart.org/sites/default/files/Owlish%20Media%20Sound%20Effects.zip', '57d7059fe2c0ea5f47c7554ace22a538ea22ab40bfc0906b71aa259afffd3d18', 'OwlishMedia / owlstorm', 'https://opengameart.org/content/sound-effects-pack'),
    'swishes': ('swishes.zip', 'https://opengameart.org/sites/default/files/swishes.zip', '7980215241b739a787dcf26f660ce510bb237900968789395a86911619795693', 'artisticdude', 'https://opengameart.org/content/swishes-sound-pack'),
    'ignition': ('ignition.flac', 'https://opengameart.org/sites/default/files/ignition.flac', 'bd8281bcee65bc705755fd36bdf40d04fadddd474f52fd481233523e178b4ea1', 'qubodup', 'https://opengameart.org/content/flare-ignition'),
    'fire': ('fire.wav', 'https://opengameart.org/sites/default/files/fire-1.wav', '26702de8a195bcbafeae72034861d875a1b7917168a25106f4e1957ecdddf7e5', 'AntumDeluge', 'https://opengameart.org/content/fire-crackling'),
    'splash1': ('splash1.wav', 'https://opengameart.org/sites/default/files/splash1_0.wav', 'e45d466f7e63e5d566f8a822a7a1a3f7d5131319d17ca0436dcc615b251e9277', 'Peludo', 'https://opengameart.org/content/water-splash-and-sand-footsteps'),
    'splash2': ('splash2.wav', 'https://opengameart.org/sites/default/files/splash2_0.wav', 'ad9472b711666de2d43c3b102d155a5d1118b1fcd0ecd29cb63177256ab63f31', 'Peludo', 'https://opengameart.org/content/water-splash-and-sand-footsteps'),
}


def recipes():
    cues = []
    def cue(path, source, member='', duration=.3, peak=.35, rate=1, start=0, lowpass=6500):
        cues.append(dict(path=path, source=source, member=member, duration=duration, peak=peak,
                         rate=rate, start=start, lowpass=lowpass))
    for i in range(1, 5):
        cue(f'Player/footstep_walk_{i:02}.wav', 'owlish', f'Footsteps/hard-footstep{i}.wav', .28, .30)
        cue(f'Player/footstep_sprint_{i:02}.wav', 'owlish', f'Footsteps/hard-footstep{i}.wav', .23, .40, 1.08)
        cue(f'Surfaces/Wood/footstep_wood_walk_{i:02}.ogg', 'rpg', f'OGG/footstep{i-1:02}.ogg', .32, .32, .94)
        cue(f'Surfaces/Wood/footstep_wood_sprint_{i:02}.ogg', 'rpg', f'OGG/footstep{i+3:02}.ogg', .27, .40, 1.04)
        cue(f'Surfaces/Metal/footstep_metal_walk_{i:02}.wav', 'owlish', f'Impacts/alt-toasterstep{i}.wav', .32, .29, .94)
        cue(f'Surfaces/Metal/footstep_metal_sprint_{i:02}.wav', 'owlish', f'Impacts/alt-toasterstep{i}.wav', .27, .36, 1.06)
        cue(f'Surfaces/Ethereal/footstep_ethereal_walk_{i:02}.ogg', 'interface', f'Audio/glass_{i:03}.ogg', .38, .19, .80, lowpass=4800)
        cue(f'Surfaces/Ethereal/footstep_ethereal_sprint_{i:02}.ogg', 'interface', f'Audio/glass_{i:03}.ogg', .32, .24, .92, lowpass=4800)
        for sprint in [False, True]:
            cue(f'Surfaces/Water/footstep_water_{"sprint" if sprint else "walk"}_{i:02}.wav',
                'splash1' if i % 2 else 'splash2', duration=.30 if sprint else .36,
                peak=.34 if sprint else .26, rate=.94 + i * .035 + (.08 if sprint else 0), start=.02 * (i//3))
    cue('Player/jump_takeoff.wav', 'rpg', 'OGG/cloth2.ogg', .25, .23)
    cue('Player/landing_soft.wav', 'owlish', 'Footsteps/hard-footstep3.wav', .30, .36, .85)
    cue('Player/landing_hard.wav', 'owlish', 'Footsteps/stumble.wav', .48, .48, .90)
    cue('Player/exhaustion_breath.wav', 'owlish', 'Human/breath-male.wav', 3.0, .20, lowpass=5000)
    cue('Player/skid_scuff.wav', 'owlish', 'Impacts/scrape2.wav', .22, .24)
    cue('Player/air_draft.wav', 'swishes', 'swishes/swish-9.wav', .48, .15, .55)
    cue('Lantern/flare_ignite.wav', 'ignition', duration=.85, peak=.49, lowpass=7200)
    cue('Lantern/flare_extinguish.wav', 'fire', duration=.40, peak=.22, start=.8, lowpass=4800)
    cue('Lantern/ember_break.wav', 'fire', duration=.26, peak=.38, start=.35)
    cue('Environment/ember_pickup.wav', 'rpg', 'OGG/handleCoins2.ogg', .38, .27, 1.05)
    cue('Environment/chain_clink_01.wav', 'rpg', 'OGG/metalLatch.ogg', .30, .27)
    cue('Environment/chain_clink_02.wav', 'rpg', 'OGG/metalClick.ogg', .26, .24, .92)
    cue('Environment/crystal_charge.wav', 'rpg', 'OGG/metalPot2.ogg', .65, .23, .72, lowpass=4600)
    cue('Environment/crystal_activate.wav', 'rpg', 'OGG/metalPot1.ogg', .85, .31, .83, lowpass=5200)
    cue('Environment/gate_open.wav', 'rpg', 'OGG/doorOpen_1.ogg', .85, .39, .80)
    cue('Environment/gate_close.wav', 'rpg', 'OGG/doorClose_2.ogg', .72, .42, .85)
    cue('UI/hover.wav', 'rpg', 'OGG/cloth1.ogg', .12, .16)
    cue('UI/confirm.wav', 'rpg', 'OGG/metalClick.ogg', .18, .22, .92)
    cue('UI/back.wav', 'rpg', 'OGG/beltHandle1.ogg', .22, .19)
    cue('UI/pause_open.wav', 'rpg', 'OGG/bookOpen.ogg', .38, .23)
    cue('UI/pause_close.wav', 'rpg', 'OGG/bookClose.ogg', .32, .21)
    cue('Controller/ignite_whoosh.mp3', 'ignition', duration=.65, peak=.40)
    cue('Controller/extinguish_whoosh.mp3', 'fire', duration=.40, peak=.22, start=.8, lowpass=4800)
    cue('Controller/pickup_crystal.mp3', 'rpg', 'OGG/handleCoins2.ogg', .35, .28)
    cue('Controller/break_glass.ogg', 'fire', duration=.30, peak=.32, start=.35)
    cue('Controller/preview_chime.ogg', 'rpg', 'OGG/metalPot1.ogg', .45, .32, .92)
    # Keep the shared completion profile and its existing GUID intact.
    cue('../../Completion/ArchiveArrival.wav', 'rpg', 'OGG/metalPot3.ogg', 1.25, .29, .68, lowpass=4600)
    return cues


def build(cache):
    cache.mkdir(parents=True, exist_ok=True)
    data = {}
    for key, (filename, url, digest, _, _) in SOURCES.items():
        path = cache / filename
        if not path.exists():
            with urllib.request.urlopen(url, timeout=60) as response:
                path.write_bytes(response.read())
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != digest:
            raise ValueError(f'Source checksum mismatch: {filename}')
        data[key] = raw
    report = []
    with tempfile.TemporaryDirectory() as folder:
        temp = Path(folder)
        for cue in recipes():
            raw = data[cue['source']]
            if cue['member']:
                raw = zipfile.ZipFile(io.BytesIO(raw)).read(cue['member'])
            source = temp / ('source' + (Path(cue['member']).suffix or '.wav'))
            source.write_bytes(raw)
            filters = f"atrim=start={cue['start']},asetpts=PTS-STARTPTS,highpass=f=70,lowpass=f={cue['lowpass']},aresample=44100,asetrate=44100*{cue['rate']},aresample=44100"
            decoded = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(source), '-ar', '44100', '-ac', '1', '-af', filters, '-f', 'f32le', '-'])
            samples = array.array('f', decoded)
            peak = max(map(abs, samples), default=0)
            if peak < .00001:
                raise ValueError(f'Empty source: {cue}')
            # Remove silent head, keeping 3ms before the first audible transient.
            first = next(i for i, v in enumerate(samples) if abs(v) > peak * .012)
            samples = samples[max(0, first-132):][:round(cue['duration']*44100)]
            peak = max(map(abs, samples))
            gain = min(4, cue['peak'] / peak)
            pcm = array.array('h')
            for i, value in enumerate(samples):
                fade = min(1, i / 132, (len(samples)-1-i) / 660)
                pcm.append(round(max(-1, min(1, value*gain*fade))*32767))
            wav = temp / 'edited.wav'
            with wave.open(str(wav), 'wb') as writer:
                writer.setparams((1, 2, 44100, 0, 'NONE', 'not compressed'))
                writer.writeframes(pcm.tobytes())
            output = (OUTPUT / cue['path']).resolve()
            output.parent.mkdir(parents=True, exist_ok=True)
            if output.suffix == '.wav':
                output.write_bytes(wav.read_bytes())
            elif output.suffix == '.mp3':
                subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(wav), '-c:a', 'libmp3lame', '-b:a', '192k', str(output)], check=True)
            else:
                soundfile.write(str(output), pcm, 44100, format='OGG', subtype='VORBIS')
            report.append(dict(cue, path=str(output.relative_to(ROOT)), source_sha256=hashlib.sha256(raw).hexdigest(),
                               output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(), samples=len(samples), sample_rate=44100))
    manifest = dict(license='CC0-1.0', sources={k:dict(filename=v[0],download=v[1],sha256=v[2],author=v[3],page=v[4]) for k,v in SOURCES.items()}, cues=report)
    (OUTPUT / 'archive-sfx-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
    print(f'Edited {len(report)} licensed cues; original asset GUIDs retained.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, default=ROOT/'Game/Library/AudioSources')
    build(parser.parse_args().cache)
