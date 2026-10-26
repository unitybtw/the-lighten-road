#!/usr/bin/env python3
"""Build short CBR Opus packets for the Bluetooth controller speaker (requires ffmpeg)."""
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
    with tempfile.TemporaryDirectory() as directory:
        temp = Path(directory)
        # Same extinguish waveform as SoundEffectManager, baked once; no runtime codec required.
        extinguish = temp / 'extinguish.wav'
        with wave.open(str(extinguish), 'wb') as wav:
            wav.setnchannels(1)
            wav.setsampwidth(2)
            wav.setframerate(48000)
            values = []
            for sample in range(round(.32 * 48000)):
                t = sample / 48000
                value = (math.sin(2 * math.pi * (180 - t * 240) * t) * .6 +
                         math.sin(2 * math.pi * 90 * t) * .4) * math.exp(-10 * t)
                values.append(round(value * 32767))
            wav.writeframes(struct.pack('<' + 'h' * len(values), *values))
        sources = {
            'Ignite': (AUDIO / 'SFX/Lantern/flare_ignite.wav', .65),
            'Extinguish': (extinguish, .32),
            'Pickup': (AUDIO / 'SFX/Environment/ember_pickup.wav', .26),
            'Break': (AUDIO / 'SFX/Lantern/ember_break.wav', .38),
        }
        for name, (source, duration) in sources.items():
            output = temp / (name + '.opus')
            # Hardware consumes 480 decoded samples per 512-sample transport interval.
            # Tempo compensation retains the original duration. Quiet gain + hardware cap prevent harsh detail.
            filters = (f'atrim=duration={duration},asetpts=PTS-STARTPTS,highpass=f=200,lowpass=f=5000,'
                       f'volume=0.22,afade=t=in:d=0.008,afade=t=out:st={max(0, duration - .06)}:d=0.06,'
                       'atempo=1.0666666667,adelay=20:all=1,apad=pad_dur=0.06')
            subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(source),
                            '-af', filters, '-ar', '48000', '-ac', '2', '-c:a', 'libopus',
                            '-b:a', '160k', '-vbr', 'off', '-frame_duration', '10', '-application', 'audio',
                            '-y', str(output)], check=True)
            frames = packets(output.read_bytes())
            if len(frames) % 2:
                frames.append(frames[-1])  # Final encoded silence, not zero-filled compressed data.
            data = b''.join(frames)
            (OUTPUT / (name + '.bytes')).write_bytes(data)
            print(f'{name}: {len(frames)} packets, {len(data)} bytes')

            # macOS HID control writes have much higher latency than the audio clock.
            # Keep controller-only details very brief; the full sound remains on normal game audio.
            short_dir = OUTPUT / 'Mac'
            short_dir.mkdir(exist_ok=True)
            short_duration = .055 if name in ('Ignite', 'Break') else .04
            pcm = temp / 'source.wav'
            subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(source),
                            '-ar', '48000', '-ac', '1', '-c:a', 'pcm_s16le', '-y', str(pcm)], check=True)
            with wave.open(str(pcm), 'rb') as wav:
                samples = array.array('h', wav.readframes(wav.getnframes()))
            window = int(short_duration * 48000)
            # Select an audible transient instead of accidentally baking leading silence.
            peak = max(range(0, max(1, len(samples) - window), 240),
                       key=lambda start: sum(x*x for x in samples[start:start+window]))
            filters = (f'atrim=start={peak / 48000}:duration={short_duration},asetpts=PTS-STARTPTS,'
                       'highpass=f=200,lowpass=f=5000,volume=0.22,afade=t=in:d=0.003,'
                       f'afade=t=out:st={short_duration - .012}:d=0.012,atempo=1.0666666667,apad=pad_dur=0.008')
            subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(source),
                            '-af', filters, '-ar', '48000', '-ac', '2', '-c:a', 'libopus',
                            '-b:a', '160k', '-vbr', 'off', '-frame_duration', '10', '-application', 'audio',
                            '-y', str(output)], check=True)
            short_frames = packets(output.read_bytes())
            if len(short_frames) % 2:
                short_frames.append(short_frames[-1])
            if len(short_frames) > 8:
                raise ValueError('Mac detail exceeds four Bluetooth audio reports')
            (short_dir / (name + '.bytes')).write_bytes(b''.join(short_frames))
            print(f'Mac/{name}: {len(short_frames)} packets')



if __name__ == '__main__':
    build()
