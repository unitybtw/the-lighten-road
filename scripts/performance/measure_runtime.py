#!/usr/bin/env python3
"""Warm Editor microbenchmarks; run in Play Mode with FirstLightTutorial open.

This is NOT a frame-rate benchmark. Positive Mono used-heap deltas are coarse,
not a complete allocation counter (small allocations may be missed). The script
pauses the tutorial, opens/closes the reader, and explicitly collects garbage
outside timed intervals. Stop Play Mode afterwards to restore the scene state.
"""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=ROOT / 'Game/Library/Readability/runtime-performance-current.json')
args = parser.parse_args()
code = Path(__file__).with_name('runtime_hotpaths.cs.txt').read_text()
response = subprocess.run(['unity', 'command', 'script', 'eval', '--code', code,
                           '--project-path', str(ROOT / 'Game'), '--json'],
                          capture_output=True, text=True, timeout=90, check=False)
try:
    envelope = json.loads(response.stdout)
except json.JSONDecodeError:
    raise SystemExit(response.stderr or response.stdout)
result = (envelope.get('data') or {}).get('result') or {}
if not envelope.get('success') or not result.get('success'):
    raise SystemExit(json.dumps(envelope.get('errors') or result, indent=2))
measurements = json.loads(result['result'])
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(measurements, indent=2) + '\n')
print(args.output)
for sample in measurements:
    print(f"{sample['label']}: {sample['microsecondsPerCall']:.1f} us/call")
