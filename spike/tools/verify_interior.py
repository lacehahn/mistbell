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
    run(name, ['caffeinate', '-dimu', '-t', '180', str(APP), '-force-gfx-direct', *args,
        '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)])
    text = log.read_text(errors='replace')
    for line in text.splitlines():
        if re.match(r'^\[(INT_FIX|INT_VISIBLE|INT_POSE|INTERIOR_U|POI|PLAY|MOVE|REACH|CINEMA|COLLIDE)\]', line):
            print(line, flush=True)
    assert not re.search(r'(Exception:|\[INTERIOR_U\].*failed)', text), name
    return text


def main():
    LOGS.mkdir(exist_ok=True)
    for name in ('interior_entry', 'interior_inside', 'interior_poi_p04'):
        (SHOTS / (name + '.png')).unlink(missing_ok=True)
    for name, args in [('entry', ['-demo-enter-church']), ('p04', ['-demo-poi', 'gergeti_p04'])]:
        text = demo(name, *args)
        match = re.search(r'\[INTERIOR_U\] door_open_deg=([\d.]+) walk_in=true player_inside=true fov_in=32 lights=6 light_shafts=([34]) colliders=(\d+) pois=3', text)
        assert match and float(match[1]) >= 95 and int(match[3]) >= 15, name
        camera = re.search(r'cam_min_y=([\d.]+) cam_wall_gap=([\d.]+) cam_below_floor=false', text)
        assert camera and float(camera[1]) >= 1.699 and float(camera[2]) >= .349, name
        lighting = re.search(r'interior_luma=([\d.]+) furniture_visible=pews:true/altar:true/iconostasis:true/chandelier:true pew_count=6', text)
        assert lighting and float(lighting[1]) >= .18, name
        p = re.search(r'player=\(([-\d.]+),([-\d.]+),([-\d.]+)\) floor_y=([-\d.]+)', text)
        assert p and -4.5 <= float(p[1]) <= 4.5 and -9 <= float(p[3]) <= 7 and abs(float(p[2])-float(p[4])) <= .3
    for name, args in [('walk', ['-demo-walk', '6']), ('stairs', ['-demo-walk-stairs']),
        ('tower', ['-demo-walk-to-tower']), ('cinema', ['-demo-cinema'])]:
        demo(name, *args)
    run('capture', ['env', 'MISTBELL_GFX_DIRECT=1', 'caffeinate', '-dimu', '-t', '180', 'bash', str(ROOT / 'spike/tools/capture_runtime.sh')], cwd=ROOT)
    for name in ('interior_entry', 'interior_inside', 'interior_poi_p04', 'art_runtime', 'art_close', 'art_ground'):
        shot = SHOTS / (name + '.png')
        assert shot.is_file() and shot.stat().st_size > 0, name
        print('[FRAME] file=%s md5=%s bytes=%d' % (shot.relative_to(PROJECT), hashlib.md5(shot.read_bytes()).hexdigest(), shot.stat().st_size), flush=True)
        result = subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(shot)], capture_output=True, text=True, check=True)
        print(result.stdout.strip(), flush=True)
        assert 'magenta_px=0 ' in result.stdout, name
    # Independently measure the saved PNG's central 40% (display-referred Rec.709).
    from measure_shot import load
    data, width, height = load(str(SHOTS / 'interior_inside.png'))
    luma_sum = 0.0
    count = 0
    for y in range(height*3//10, height*7//10):
        for x in range(width*3//10, width*7//10):
            i = (y*width+x)*3
            luma_sum += (.2126*data[i]+.7152*data[i+1]+.0722*data[i+2])/255
            count += 1
    luma = luma_sum/count
    print('[INT_FIX_PNG] center40_luma=%.6f' % luma, flush=True)
    assert luma >= .18
    print('[VERIFY_INTERIOR] ALL PASS', flush=True)


if __name__ == '__main__':
    main()
