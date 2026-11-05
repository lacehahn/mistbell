#!/usr/bin/env python3
"""Fix1 gates, including independent CIE L* measurements of final screen pixels."""
from pathlib import Path
import json
import re
import subprocess
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'spike/artifacts/climb2d'
log = (ROOT / 'spike/logs/climb2d_run.log').read_text()

def need(ok, message):
    if not ok:
        raise SystemExit('FAIL: ' + message)

def lightness(rgb):
    c = [v / 255 for v in rgb]
    r, g, b = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in c]
    y = .2126729 * r + .7151522 * g + .072175 * b
    f = y ** (1 / 3) if y > .008856 else 7.787 * y + 16 / 116
    return 116 * f - 16

floats = re.findall(r'\[FLOAT\] name=(\S+) pos=([^\n]+) removed=true', log)
need(len(floats) == 2, 'two identified misplaced platform instances removed')
shadow = re.search(r'\[SLOPE_SHADOW\] band_h=(\d+) alpha=([\d.]+) follows_curve=true colour=#3A4048 edge_px=(\d+)', log)
need(shadow is not None, 'shadow/edge evidence')
height, alpha, edge = map(float, shadow.groups())
need(40 <= height <= 70 and .35 <= alpha <= .5 and 3 <= edge <= 5, 'shadow/edge bounds')
results = []
for name in ('start', 'ramp_mid', 'turn'):
    shot = Image.open(OUT / f'climb2d_{name}.png').convert('RGB')
    need(shot.size == (1920, 1080), name + ' dimensions')
    mask = Image.open(OUT / f'contrast_mask_{name}.png').convert('RGB')
    need(mask.size == (480, 270), name + ' mask dimensions')
    values = {'path': [], 'snow': []}
    for y in range(270):
        for x in range(480):
            mark = mask.getpixel((x, y))
            kind = 'path' if mark == (255, 0, 0) else 'snow' if mark == (0, 255, 0) else None
            if kind:
                values[kind].append(lightness(shot.getpixel((x * 4 + 2, y * 4 + 2))))
    need(all(values.values()), name + ' nonempty samples')
    path, snow = [sum(values[k]) / len(values[k]) for k in ('path', 'snow')]
    delta = snow - path
    need(delta >= 12, name + ' contrast >=12 L*')
    measured = re.search(r'\[PATH_CONTRAST\] shot=' + name + r' path_L=([\d.]+) snow_L=([\d.]+) delta=([\d.]+) edge_dark=true', log)
    need(measured is not None, name + ' runtime contrast evidence')
    need(all(abs(a - b) < .02 for a, b in zip((path, snow, delta), map(float, measured.groups()))), name + ' screenshot agrees with framebuffer')
    results.append(dict(shot=name, path_L=path, snow_L=snow, delta=delta, path_samples=len(values['path']), snow_samples=len(values['snow'])))
    print(f'[PATH_CONTRAST] shot={name} path_L={path:.3f} snow_L={snow:.3f} delta={delta:.3f} edge_dark=true samples={len(values["path"])}/{len(values["snow"])}')
subprocess.run([sys.executable, str(ROOT / 'spike/tools/verify_climb2d.py')], check=True)
(OUT / 'fix1_acceptance.json').write_text(json.dumps(dict(removed_instances=len(floats), band_h=height, alpha=alpha, edge_px=edge, contrast=results), indent=2) + '\n')
print(f'[FLOAT] removed=true instances={len(floats)}')
print(f'[SLOPE_SHADOW] band_h={height:g} alpha={alpha:g} follows_curve=true')
print('ACCEPT climb2d_fix1')
