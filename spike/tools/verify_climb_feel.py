#!/usr/bin/env python3
"""Task-only acceptance. Run after rebuild_all.sh and PerformanceBuild.Run."""
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'spike/project'
APP = PROJECT / 'Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell'
OUT = ROOT / 'spike/artifacts/climb_feel'
OUT.mkdir(parents=True, exist_ok=True)


def run(name, flags, timeout=180):
    log = OUT / (name + '.log')
    cmd = [str(APP), '-force-gfx-direct', '-perf-unpaced', *flags,
           '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)]
    print('[RUN] ' + ' '.join(cmd), flush=True)
    with (OUT / (name + '.stdout')).open('w') as f:
        try:
            rc = subprocess.run(cmd, cwd=PROJECT, stdout=f, stderr=f, timeout=timeout).returncode
        except subprocess.TimeoutExpired:
            rc = 124
    body = log.read_text() if log.exists() else ''
    for line in body.splitlines():
        if line.startswith(('[CLIMB', '[PLAY]', '[BENCH', '[INTERIOR_U]', '[INT_TERMS]', '[REVEAL]', '[CLIMB]')):
            print(line, flush=True)
    print(f'[EXIT] {name}={rc}', flush=True)
    return rc, body


def main():
    rebuild = (ROOT / 'spike/logs/climb_rebuild.log').read_text()
    geometry = (ROOT / 'spike/logs/rebuild/22_performance.log').read_text()
    results = {'rebuild_22': '[REBUILD] 22/22 pass' in rebuild and '[REBUILD] done fail=0' in rebuild,
               'geometry': '[CLIMB_GEOMETRY]' in geometry and 'protected_unchanged=True' in geometry}
    for line in geometry.splitlines():
        if line.startswith(('[CLIMB_STEPS]', '[CLIMB_GEOMETRY]')):
            print(line, flush=True)
    for name, flags in [
        ('walk6', ['-demo-walk', '6']),
        ('steps', ['-demo-climb-feel']),
        ('enter', ['-demo-enter-church']),
        ('reveal', ['-demo-reveal']),
        ('climb', ['-demo-climb']),
        ('bench', ['-bench', '-bench-output', str(OUT / 'bench.json')]),
    ]:
        rc, log = run(name, flags)
        results[name] = {'rc': rc}
        if name == 'walk6':
            results[name]['bad_zero'] = bool(re.search(r'\[CLIMB_WALK6\] samples=\d+ bad=0\b', log))
        if name == 'steps':
            results[name]['three_pass'] = bool(re.search(r'\[CLIMB_WALK\] flights=3 samples=\d+ bad=0\b', log))
    bench = json.loads((OUT / 'bench.json').read_text())['results']
    results['outdoor_fps'] = {r['pos']: r['fps_avg'] for r in bench if r['pos'] != 'interior'}
    images = []
    for name in ('climb_steps.png', 'play_view.png'):
        path = PROJECT / 'Shots' / name
        shutil.copy2(path, OUT / name)
        images.append(path.exists() and path.stat().st_size > 10000)
    passed = (all(results[n]['rc'] == 0 for n in ('walk6', 'steps', 'enter', 'reveal', 'climb', 'bench'))
              and results['rebuild_22'] and results['geometry']
              and results['walk6']['bad_zero'] and results['steps']['three_pass']
              and all(fps >= 200 for fps in results['outdoor_fps'].values()) and all(images))
    results['pass'] = passed
    (OUT / 'acceptance.json').write_text(json.dumps(results, indent=2) + '\n')
    print('[CLIMB_ACCEPTANCE] ' + json.dumps(results), flush=True)
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
