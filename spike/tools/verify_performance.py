#!/usr/bin/env python3
"""Run the 1080p performance/demo/capture gates against PerformanceBuild.Run's player."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'spike/project'
LOGS = ROOT / 'spike/logs'
APP = PROJECT / 'Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell'


def main():
    LOGS.mkdir(exist_ok=True)
    codes = []
    for flag in ('-bench', '-demo-enter-church', '-demo-reveal', '-demo-walk-to-tower'):
        log = LOGS / ('perf_after.log' if flag == '-bench' else 'perf_' + flag.lstrip('-') + '.log')
        cmd = [str(APP), '-force-gfx-direct', '-perf-unpaced', flag, '-screen-width', '1920', '-screen-height', '1080',
               '-screen-fullscreen', '0', '-logFile', str(log)]
        if flag == '-bench':
            cmd += ['-bench-before', str(LOGS / 'perf_before.json'), '-bench-output', str(LOGS / 'perf_after.json')]
        print('[RUN] ' + ' '.join(cmd), flush=True)
        with (LOGS / (log.stem + '.stdout')).open('w') as output:
            try:
                rc = subprocess.run(cmd, cwd=PROJECT, stdout=output, stderr=output, timeout=180).returncode
            except subprocess.TimeoutExpired:
                rc = 124
        codes.append(rc)
        print(f'[EXIT] {flag}={rc}', flush=True)
        for line in log.read_text().splitlines():
            if line.startswith(('[BENCH', '[STATIC_BATCH]', '[INTERIOR_U]', '[INT3]', '[REVEAL]', '[REACH]', '[PLAY]')):
                print(line, flush=True)
    cmd = ['bash', str(ROOT / 'spike/tools/capture_runtime.sh')]
    print('[RUN] ' + ' '.join(cmd), flush=True)
    result = subprocess.run(cmd, cwd=ROOT, env={**os.environ, 'MISTBELL_GFX_DIRECT': '1'})
    codes.append(result.returncode)
    print(f'[EXIT] capture_runtime.sh={result.returncode}', flush=True)
    paths = sorted((LOGS / 'perf_after_frames').glob('*.png'))
    paths += [PROJECT / 'Shots' / name for name in (
        'art_runtime.png', 'art_close.png', 'art_ground.png',
        'interior_entry.png', 'interior_inside.png', 'reveal_20.png', 'reveal_50.png', 'reveal_80.png')]
    for path in paths:
        print(f'[FRAME] file={path.relative_to(ROOT)} md5={hashlib.md5(path.read_bytes()).hexdigest()}', flush=True)
        # Same unmodified measurement program, with its normal MEASURE output.
        cmd = [sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(path)]
        result = subprocess.run(cmd, capture_output=True, text=True)
        print(result.stdout.strip(), flush=True)
        codes.append(result.returncode)
        if 'magenta_px=0 ' not in result.stdout or 'size=1920x1080' not in result.stdout:
            codes.append(1)
        print(f'[EXIT] measure_shot.py:{path.name}={result.returncode}', flush=True)
    bench = json.loads((LOGS / 'perf_after.json').read_text())['results']
    gate = len(bench) == 3 and all(r['fps_avg'] >= 55 and 0 < r['drawcalls'] <= 250 and r['tris'] > 0 for r in bench)
    passed = all(code == 0 for code in codes) and gate
    print(f'[PERF_ACCEPTANCE] pass={str(passed).lower()} commands={len(codes)} bench_gate={gate}', flush=True)
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
