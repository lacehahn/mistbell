#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Window-player interior gates + unchanged exterior regressions. Build first."""
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


def run(name, command, cwd=PROJECT, timeout=120):
    log = LOGS / ('interior_verify_' + name + '.log')
    with log.open('w') as output:
        result = subprocess.run(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
    print('[VERIFY_INTERIOR] %s exit=%d log=%s' % (name, result.returncode, log.relative_to(ROOT)), flush=True)
    assert result.returncode == 0, name
    return log.read_text(errors='replace')


def demo(name, *args):
    log = LOGS / ('interior_runtime_' + name + '.log')
    run(name, ['caffeinate', '-dimu', '-t', '180', str(APP), *args,
        '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)])
    text = log.read_text(errors='replace')
    for line in text.splitlines():
        if re.match(r'^\[(INTERIOR_U|POI|PLAY|MOVE|REACH|CINEMA|COLLIDE)\]', line):
            print(line, flush=True)
    assert not re.search(r'(Exception:|\[INTERIOR_U\].*failed)', text), name
    return text


def main():
    LOGS.mkdir(exist_ok=True)
    for name in ('interior_entry', 'interior_inside', 'interior_poi_p04'):
        (SHOTS / (name + '.png')).unlink(missing_ok=True)
    for name, args in [('entry', ['-demo-enter-church']), ('p05', ['-demo-poi', 'gergeti_p05']),
        ('p06', ['-demo-poi', 'gergeti_p06']), ('p04', ['-demo-poi', 'gergeti_p04'])]:
        text = demo(name, *args)
        match = re.search(r'\[INTERIOR_U\] door_open_deg=([\d.]+) walk_in=true player_inside=true fov_in=32 lights=5 light_shafts=([34]) colliders=(\d+) pois=3', text)
        assert match and float(match[1]) >= 95 and int(match[3]) >= 15, name
        p = re.search(r'player=\(([-\d.]+),([-\d.]+),([-\d.]+)\) floor_y=([-\d.]+)', text)
        assert p and -4.5 <= float(p[1]) <= 4.5 and -9 <= float(p[3]) <= 7 and abs(float(p[2])-float(p[4])) <= .3
    for name, args in [('walk', ['-demo-walk', '6']), ('stairs', ['-demo-walk-stairs']),
        ('tower', ['-demo-walk-to-tower']), ('cinema', ['-demo-cinema'])]:
        demo(name, *args)
    run('capture', ['caffeinate', '-dimu', '-t', '180', 'bash', str(ROOT / 'spike/tools/capture_runtime.sh')], cwd=ROOT)
    for name in ('interior_entry', 'interior_inside', 'interior_poi_p04', 'art_runtime', 'art_close', 'art_ground'):
        shot = SHOTS / (name + '.png')
        assert shot.is_file() and shot.stat().st_size > 0, name
        print('[FRAME] file=%s md5=%s bytes=%d' % (shot.relative_to(PROJECT), hashlib.md5(shot.read_bytes()).hexdigest(), shot.stat().st_size), flush=True)
        result = subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(shot)], capture_output=True, text=True, check=True)
        print(result.stdout.strip(), flush=True)
        assert 'magenta_px=0 ' in result.stdout, name
    print('[VERIFY_INTERIOR] ALL PASS', flush=True)


if __name__ == '__main__':
    main()
