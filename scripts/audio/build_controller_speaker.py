#!/usr/bin/env python3
"""Build crystal-clear, authentic CBR Opus packets for the DualSense controller speaker (requires ffmpeg).

Acoustically tuned for the physical frequency response and micro-transducer limits of the DualSense:
- Uses authentic, studio-recorded acoustic source assets (Kenney CC0 & open sound library).
- 380 Hz 2-pole highpass eliminates mechanical sub-bass cone rattling and voice-coil bottoming out.
- 5600 Hz 2-pole lowpass removes harsh high-frequency distortion and DAC hiss.
- Transparent peak limiter with soft attack/release prevents clipping and distortion.
"""
import math
from pathlib import Path
import struct
import subprocess
import tempfile
import array
import wave

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / 'Game/Assets/_Project/Resources/Audio'
OUTPUT = AUDIO / 'ControllerSpeaker'
SFX_CTRL = AUDIO / 'SFX/Controller'


def packets(data):
    position = 0
    partial = bytearray()
    result = []
    while position < len(data):
        if data[position:position + 4] != b'OggS':
            raise ValueError('Invalid Ogg page')
        count = data[position + 26]
        sizes = data[position + 27:position + 27 + count]
        payload = position + 27 + count
        for size in sizes:
            partial.extend(data[payload:payload + size])
            payload += size
            if size < 255:
                if not partial.startswith((b'OpusHead', b'OpusTags')):
                    if len(partial) != 200:
                        raise ValueError(f'Expected 200-byte CBR packet, got {len(partial)}')
                    result.append(bytes(partial))
                partial.clear()
        position = payload
    if partial:
        raise ValueError('Truncated Opus packet')
    return result


def build():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    short_dir = OUTPUT / 'Mac'
    short_dir.mkdir(parents=True, exist_ok=True)

    sources = {
        'Ignite': {
            'file': SFX_CTRL / 'ignite_whoosh.mp3',
            'full_dur': 0.65,
            'mac_dur': 0.28,
            'vol': 0.85,
        },
        'Extinguish': {
            'file': SFX_CTRL / 'extinguish_whoosh.mp3',
            'full_dur': 0.40,
            'mac_dur': 0.24,
            'vol': 0.80,
        },
        'Pickup': {
            'file': SFX_CTRL / 'pickup_crystal.mp3',
            'full_dur': 0.35,
            'mac_dur': 0.22,
            'vol': 0.85,
        },
        'Break': {
            'file': SFX_CTRL / 'break_glass.ogg',
            'full_dur': 0.30,
            'mac_dur': 0.24,
            'vol': 0.70,
        },
        'Preview': {
            'file': SFX_CTRL / 'preview_chime.ogg',
            'full_dur': 0.45,
            'mac_dur': 0.40,
            'vol': 0.75,
        },
    }

    with tempfile.TemporaryDirectory() as directory:
        temp = Path(directory)

        for name, cfg in sources.items():
            source = cfg['file']
            full_dur = cfg['full_dur']
            mac_dur = cfg['mac_dur']
            vol = cfg['vol']

            # 1. Full cue for standard playback
            full_opus = temp / f'{name}_full.opus'
            full_filters = (
                f'atrim=duration={full_dur},asetpts=PTS-STARTPTS,'
                'highpass=f=380:poles=2,lowpass=f=5600:poles=2,'
                f'volume={vol:.2f},alimiter=limit=0.68:attack=8:release=45:asc=true,'
                f'afade=t=in:d=0.010,afade=t=out:st={max(0, full_dur - 0.05)}:d=0.05,'
                'atempo=1.0666666667,adelay=20:all=1,apad=pad_dur=0.06'
            )
            subprocess.run([
                'ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(source),
                '-af', full_filters, '-ar', '48000', '-ac', '2', '-c:a', 'libopus',
                '-b:a', '160k', '-vbr', 'off', '-frame_duration', '10', '-application', 'audio',
                '-y', str(full_opus)
            ], check=True)

            frames = packets(full_opus.read_bytes())
            if len(frames) % 2:
                frames.append(frames[-1])
            data = b''.join(frames)
            (OUTPUT / f'{name}.bytes').write_bytes(data)
            print(f'{name}: {len(frames)} packets, {len(data)} bytes')

            # 2. Tailored Mac cue (must strictly have between 20 and 52 packets, i.e., 4000 to 10400 bytes)
            mac_opus = temp / f'{name}_mac.opus'
            mac_filters = (
                f'atrim=duration={mac_dur},asetpts=PTS-STARTPTS,'
                'highpass=f=400:poles=2,lowpass=f=5600:poles=2,'
                f'volume={vol * 1.1:.2f},alimiter=limit=0.68:attack=8:release=45:asc=true,'
                f'afade=t=in:d=0.012,afade=t=out:st={max(0, mac_dur - 0.04)}:d=0.04,'
                'atempo=1.0666666667,adelay=20:all=1,apad=pad_dur=0.06'
            )
            subprocess.run([
                'ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(source),
                '-af', mac_filters, '-ar', '48000', '-ac', '2', '-c:a', 'libopus',
                '-b:a', '160k', '-vbr', 'off', '-frame_duration', '10', '-application', 'audio',
                '-y', str(mac_opus)
            ], check=True)

            mac_frames = packets(mac_opus.read_bytes())
            if len(mac_frames) % 2:
                mac_frames.append(mac_frames[-1])
            if len(mac_frames) > 52:
                raise ValueError(f'{name} mac cue exceeds bounded playback budget: {len(mac_frames)} packets')
            if len(mac_frames) < 20:
                raise ValueError(f'{name} mac cue has fewer than 20 packets: {len(mac_frames)} packets')

            mac_data = b''.join(mac_frames)
            (short_dir / f'{name}.bytes').write_bytes(mac_data)
            print(f'Mac/{name}: {len(mac_frames)} packets, {len(mac_data)} bytes')


if __name__ == '__main__':
    build()
