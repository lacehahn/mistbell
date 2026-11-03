#!/usr/bin/env python3
"""Run the built player through trail gates and the requested unchanged capture/measurement tools."""
import hashlib
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'spike/project'
LOGS = ROOT / 'spike/logs'
SHOTS = PROJECT / 'Shots'
APP = PROJECT / 'Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell'


def demo(name, *args):
    log = LOGS / f'trail_runtime_{name}.log'
    cmd = ['caffeinate', '-dimu', '-t', '180', str(APP), '-force-gfx-direct', *args,
           '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)]
    result = subprocess.run(cmd, cwd=PROJECT, capture_output=True, timeout=180)
    print(f'[VERIFY_TRAIL] {name} exit={result.returncode} log={log.relative_to(ROOT)}', flush=True)
    text = log.read_text(errors='replace')
    for line in text.splitlines():
        if re.match(r'^\[(TRAIL|TRAIL_SHOT|PLAY|MOVE|CINEMA|REVEAL|INTERIOR_U|AUDIO)\]', line):
            print(line, flush=True)
    assert result.returncode == 0, name
    assert not re.search(r'(Exception:|error CS\d+)', text), name
    return text


def short_gate(text):
    m = re.search(r'\[TRAIL\] spacing=0.75m max=120 spawns=(\d+) despawns=(\d+) ground_err_max=([\d.]+) paving_skipped=(\d+) static_kept=64', text)
    assert m and 20 <= int(m[1]) <= 26 and int(m[2]) == 0 and float(m[3]) <= .02 and int(m[4]) >= 1, text
    d = re.search(r'\[PLAY\].*distance=([\d.]+)', text)
    assert d and float(d[1]) >= 15


def main():
    LOGS.mkdir(exist_ok=True)
    (SHOTS / 'footprints_trail.png').unlink(missing_ok=True)
    short_gate(demo('walk', '-demo-walk', '6'))
    short_gate(demo('trail', '-demo-trail'))
    for name, flag in [('stairs', '-demo-walk-stairs'), ('enter', '-demo-enter-church'),
                       ('cinema', '-demo-cinema'), ('reveal', '-demo-reveal')]:
        demo(name, flag)
    capture = subprocess.run(['env', 'MISTBELL_GFX_DIRECT=1', 'bash', str(ROOT / 'spike/tools/capture_runtime.sh')], cwd=ROOT, timeout=300)
    print(f'[VERIFY_TRAIL] capture_runtime exit={capture.returncode}', flush=True)
    assert capture.returncode == 0
    for shot in [SHOTS / 'footprints_trail.png', *sorted(SHOTS.glob('art_*.png')),
                 SHOTS / 'interior_entry.png', SHOTS / 'interior_inside.png', SHOTS / 'interior_lookup.png']:
        assert shot.is_file() and shot.stat().st_size > 0, shot
        print(f'[FRAME] file={shot.relative_to(PROJECT)} md5={hashlib.md5(shot.read_bytes()).hexdigest()}', flush=True)
        measured = subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(shot)], capture_output=True, text=True)
        print(measured.stdout.strip(), flush=True)
        print(f'[VERIFY_TRAIL] measure={shot.name} exit={measured.returncode}', flush=True)
        assert measured.returncode == 0 and 'magenta_px=0 ' in measured.stdout, shot
    print('[VERIFY_TRAIL] ALL PASS', flush=True)


if __name__ == '__main__':
    main()
