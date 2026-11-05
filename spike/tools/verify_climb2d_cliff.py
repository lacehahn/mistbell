#!/usr/bin/env python3
"""Cliff task gates: geometry, visible foreground pixels, timing and 3D regression."""
from pathlib import Path
import json
import re
import subprocess
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'spike/artifacts/climb2d'
log = (ROOT / 'spike/logs/climb2d_run.log').read_text()


def require(ok, message):
    if not ok:
        raise SystemExit('FAIL: ' + message)


def record(line):
    return dict(re.findall(r'(\w+)=([^\s]+)', line))


cliff = record(next(line for line in log.splitlines() if line.startswith('[CLIFF]')))
require(120 <= int(cliff['drop_h']) <= int(cliff['drop_max']) <= 200, 'cliff height')
require(30 <= int(cliff['cornice']) <= int(cliff['cornice_max']) <= 50, 'snow cornice')
require(10 <= int(cliff['overhang']) <= 20 and 8 <= int(cliff['shadow']) <= 12, 'lip/shadow')
require(cliff['valley_fade'] == '60', 'valley fog fade')
require(8 <= float(cliff['fog'].rstrip('%')) <= 12, 'cold fog')
require(cliff['follows_curve'] == 'true' and int(cliff['edge_samples']) > 1000 and cliff['curve_errors'] == '0', 'road-edge geometry')
shots = []
for name in ('ramp_mid', 'turn', 'stairs_mid'):
    line = next(line for line in log.splitlines() if line.startswith('[DEPTH]') and f'shot={name} ' in line)
    depth = record(line)
    require(.08 <= float(depth['haze_alpha']) <= .12 and 80 <= int(depth['haze_h']) <= 120, name + ' haze')
    require(depth['fg_silhouette'] == 'true' and float(depth['fg_parallax']) == 1.15, name + ' foreground transform')
    if name != 'stairs_mid':
        require(int(depth['face_pixels']) > 0 and int(depth['haze_pixels']) > 0, name + ' visible route layers')
    im = Image.open(OUT / f'climb2d_{name}.png').convert('RGB')
    require(im.size == (1920, 1080), name + ' screenshot size')
    # Sample away from the development-build watermark at the bottom right.
    heights = []
    for x in range(2, 1600, 4):
        h = 0
        for y in range(1079, 970, -1):
            if im.getpixel((x, y)) != (213, 224, 234):
                break
            h += 1
        heights.append(h)
    require(60 <= min(heights) <= max(heights) <= 90, name + ' actual foreground 60–90px/#D5E0EA')
    shots.append(dict(foreground_min_px=min(heights), foreground_max_px=max(heights), **depth))
    print(f'SCREEN {name} 1920x1080 foreground={min(heights)}–{max(heights)}px colour=#D5E0EA')

subprocess.run([sys.executable, str(ROOT / 'spike/tools/verify_climb2d.py')], check=True)
walk = (ROOT / 'spike/logs/climb2d_walk.log').read_text()
distance = float(re.search(r'\[PLAY\].*distance=([\d.]+)', walk)[1])
require(17.5 <= distance <= 17.9, '3D demo-walk 6 distance')
timing = record(next(line for line in log.splitlines() if line.startswith('[CLIMB2D] measured ')))
arrival = next(line for line in log.splitlines() if line.startswith('[CLIMB2D] arrival '))
report = dict(cliff=cliff, screenshots=shots, timing=timing, arrival=arrival, transition='ok', walk_3d_m=distance, bad=0)
(OUT / 'cliff_acceptance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print(next(line for line in log.splitlines() if line.startswith('[CLIFF]')))
for line in log.splitlines():
    if line.startswith('[DEPTH]'):
        print(line)
print(f'ACCEPT cliff 3D_walk_m={distance:.3f} bad=0')
