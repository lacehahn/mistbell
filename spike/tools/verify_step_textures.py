#!/usr/bin/env python3
"""Only step_textures.md acceptance; no rebuild_all dependency."""
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'spike/project'
APP = PROJECT / 'Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell'
OUT = ROOT / 'spike/artifacts/step_textures'

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    build = (ROOT / 'spike/logs/step_textures_build.log').read_text()
    for line in build.splitlines():
        if line.startswith(('[STEP_TEX]', '[STEP_BUILD]')):
            print(line, flush=True)
    results = {'build': '[STEP_BUILD] result=Succeeded errors=0' in build,
               'material': '[STEP_TEX] tread=1.35 riser=1.2x1 snow=0.7' in build and '[STEP_TEX] riser_smoothness=0.08 spec=0' in build,
               'protection': all(s in build for s in ('steps=30 tread_quads=30 riser_quads=90', 'geometry_unchanged=True', 'collider_unchanged=True', 'protected_unchanged=True', 'base_color_unchanged=True'))}
    for name, flags in [('walk6', ['-demo-walk', '6']), ('steps', ['-demo-climb-feel']),
                        ('enter', ['-demo-enter-church']), ('reveal', ['-demo-reveal']),
                        ('bench', ['-bench', '-bench-foreground', '-bench-output', str(OUT / 'bench.json')])]:
        log = OUT / (name + '.log')
        cmd = [str(APP), '-force-gfx-direct', '-perf-unpaced', *flags, '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)]
        with (OUT / (name + '.stdout')).open('w') as f:
            try:
                rc = subprocess.run(cmd, cwd=PROJECT, stdout=f, stderr=f, timeout=180).returncode
            except subprocess.TimeoutExpired:
                rc = 124
        body = log.read_text() if log.exists() else ''
        for line in body.splitlines():
            if line.startswith(('[PLAY]', '[CLIMB_WALK', '[BENCH]', '[REVEAL]', '[INTERIOR_U]')):
                print(line, flush=True)
        results[name] = {'rc': rc}
        if name == 'walk6':
            results[name]['bad_zero'] = bool(re.search(r'\[CLIMB_WALK6\] samples=\d+ bad=0\b', body))
            match = re.search(r'\[PLAY\].*distance=([\d.]+)', body)
            if match:
                results[name]['distance'] = float(match[1])
        if name == 'steps':
            results[name]['three_pass'] = bool(re.search(r'\[CLIMB_WALK\] flights=3 samples=\d+ bad=0\b', body))
        print(f'[EXIT] {name}={rc}', flush=True)
    bench = json.loads((OUT / 'bench.json').read_text())['results']
    results['outdoor_fps'] = {r['pos']: r['fps_avg'] for r in bench if r['pos'] != 'interior'}
    for name in ('climb_steps.png', 'play_view.png'):
        path = PROJECT / 'Shots' / name
        shutil.copy2(path, OUT / name)
        results[name] = path.exists() and path.stat().st_size > 10000
    results['pass'] = (all(results[n]['rc'] == 0 for n in ('walk6', 'steps', 'enter', 'reveal', 'bench')) and
                       results['build'] and results['material'] and results['protection'] and
                       results['walk6']['bad_zero'] and results['steps']['three_pass'] and
                       bool(results['outdoor_fps']) and all(fps >= 200 for fps in results['outdoor_fps'].values()) and
                       results['climb_steps.png'] and results['play_view.png'])
    (OUT / 'acceptance.json').write_text(json.dumps(results, indent=2) + '\n')
    print('[STEP_ACCEPTANCE] ' + json.dumps(results), flush=True)
    return 0 if results['pass'] else 1

if __name__ == '__main__':
    raise SystemExit(main())
