#!/usr/bin/env python3
"""Grade task: independently check captured pixels, generated cuts, timing and 3D protection."""
from pathlib import Path
from functools import lru_cache
import hashlib
import json
import re
import subprocess
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'spike/artifacts/climb2d'
LOG = (ROOT / 'spike/logs/climb2d_run.log').read_text()


def need(ok, message):
    if not ok:
        raise SystemExit('FAIL: ' + message)


def fields(line):
    return dict(re.findall(r'([^\s=]+)=([^\s]+)', line))


def entry(prefix, shot=None):
    return fields(next(line for line in LOG.splitlines()
                       if line.startswith(prefix) and (shot is None or f'shot={shot} ' in line)))


@lru_cache(maxsize=None)
def lab(rgb):
    c = [v / 255 for v in rgb[:3]]
    r, g, b = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in c]

    def f(v):
        return v ** (1 / 3) if v > .008856 else 7.787 * v + 16 / 116

    fy = f(.2126729 * r + .7151522 * g + .072175 * b)
    return 116 * fy - 16, 200 * (fy - f((.0193339 * r + .119192 * g + .9503041 * b) / 1.08883))


subprocess.run([sys.executable, str(ROOT / 'spike/tools/verify_climb2d.py')], check=True)
protected = json.loads((OUT / 'grade_protected.json').read_text())
for name, digest in protected.items():
    need(hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == digest, 'protected file changed: ' + name)
print(f'PROTECTED unchanged={len(protected)} (all tracked Assets except Climb2D.cs; AGENTS.md)')

cliffs = []
for i in range(3):
    im = Image.open(OUT / f'grade_drop_{i}.png').convert('RGBA')
    w, h = im.size
    rock_h = (h - 6) * 4  # separate 24px snow cap; alpha fade is inside the rock height
    need(120 <= rock_h <= 200 and 200 <= w * 4 <= 400, f'cliff {i} dimensions')
    alpha = [im.getpixel((w // 2, h - 1 - y))[3] for y in range(h)]
    need(alpha[0] == 0 and alpha[10] == 255, f'cliff {i} 40px fade endpoints')
    need(all(0 < a < 255 for a in alpha[1:10]), f'cliff {i} continuous fade')
    # White cap must actually be present; cut source should contain textured dark rock.
    need(any(lab(im.getpixel((x, 3)))[0] > 90 for x in range(5, w - 5)), f'cliff {i} snow')
    need(len(set(im.getdata())) > 50, f'cliff {i} authored rock detail')
    cliffs.append(dict(height_px=rock_h, width_px=w * 4, snow_px=24, fade_px=40))
c = entry('[CLIFF2]')
need(c['drops'] == '3' and c['alpha_errors'] == '0', 'runtime cliff audit')
need(int(c['min_h']) == min(d['height_px'] for d in cliffs) and int(c['max_h']) == max(d['height_px'] for d in cliffs), 'cliff log agrees with exported cuts')
print('[CLIFF2] drops=3 each_h=' + '/'.join(str(d['height_px']) for d in cliffs) + ' cornice=24 fade=40')

sources = [Image.open(OUT / f'grade_tree_source_{i}.png').convert('RGBA') for i in range(3)]
need(len({im.tobytes() for im in sources}) == 3, 'three distinct tree crops')
for i, im in enumerate(sources):
    box = im.getchannel('A').getbbox()
    need(box is not None and box[2] - box[0] > im.width * .5 and box[3] - box[1] > im.height * .8, f'nonempty tree crop {i}')

screens = []
for name in ('ramp_mid', 'turn', 'stairs_mid'):
    shot = Image.open(OUT / f'climb2d_{name}.png').convert('RGB')
    need(shot.size == (1920, 1080), name + ' dimensions')
    mask = Image.open(OUT / f'grade_mask_{name}.png').convert('RGB')
    before = Image.open(OUT / f'grade_before_{name}.png').convert('RGB')
    need(mask.size == before.size == (480, 270), name + ' measurement buffers')
    snow_b, stone_b = [], []
    sx = sy = sxx = sxy = 0.
    # The native renderer is 480x270; sample centres of its 4x4 screen blocks.
    pixels = [[shot.getpixel((x * 4 + 2, (269 - y) * 4 + 2)) for x in range(480)] for y in range(270)]
    # PNG buffers are already stored top-down; use top-down coordinates here.
    for y in range(270):
        for x in range(480):
            mark = mask.getpixel((x, y))
            l, b = lab(shot.getpixel((x * 4 + 2, y * 4 + 2)))
            if mark == (0, 255, 0):
                snow_b.append(b)
            elif mark == (255, 0, 0):
                stone_b.append(b)
                a = lab(before.getpixel((x, y)))[0]
                sx += a; sy += l; sxx += a * a; sxy += a * l
    need(snow_b and stone_b, name + ' nonempty colour masks')
    snow = sum(snow_b) / len(snow_b)
    stone = sum(stone_b) / len(stone_b)
    n = len(stone_b)
    contrast = (n * sxy - sx * sy) / (n * sxx - sx * sx) - 1
    need(-2 <= snow <= 1 and 5 <= stone <= 7, name + ' Lab b* range')
    need(abs(contrast - .12) < .005, name + ' contrast increase 12% (RGB quantisation tolerance 0.5 percentage points)')
    r = entry('[GRADE]', name)
    need(abs(snow - float(r['snow_b*'])) < .003 and abs(stone - float(r['stone_b*'])) < .003, name + ' screenshot colour agrees with log')
    need(abs(contrast - float(r['contrast_measured'])) < .0001, name + ' screenshot contrast agrees with log')
    # Top row is entirely atmosphere, including side edges: no opaque drip silhouette.
    need(all(shot.getpixel((x, 2)) == (220, 230, 239) for x in range(1920)), name + ' top border #DCE6EF')
    top = entry('[TOP]', name)
    need(top['fog_px'] == '40' and top['edge_bad'] == '0' and top['crop_opaque'] == 'true', name + ' top fade/crop')
    hard = 0
    for y in range(1, 260):
        abrupt = sum(sum(abs(a - b) for a, b in zip(pixels[y - 1][x], pixels[y][x])) / 3 >= 12 for x in range(480))
        hard += abrupt >= 480 * .8
    need(hard == 0, name + ' horizontal hard seams (RGB jump>=12 across>=80% width)')
    seam = entry('[SEAM]', name)
    need(seam['hard_lines'] == '0' and seam['alpha_errors'] == '0' and seam['blend_px'] == '20', name + ' blend evidence')
    trees = [fields(line) for line in LOG.splitlines() if line.startswith('[TREE2]') and f'shot={name} ' in line]
    tree_summary = entry('[TREES]', name)
    need(tree_summary['variants'] == '3' and tree_summary['flip'] == 'true' and tree_summary['luma_jitter'] == '0.08', name + ' main tree criterion log')
    need(len(trees) == 9 and {t['variant'] for t in trees} == {'0', '1', '2'}, name + ' tree variants')
    need({int(t['height_px']) for t in trees} == {90, 150, 240}, name + ' tree scales')
    need({t['flip'] for t in trees} == {'true', 'false'}, name + ' tree flips')
    need(all(.92 <= float(t['gain']) <= 1.08 and abs(float(t['jitter_x'])) <= 12 and abs(float(t['jitter_y'])) <= 12 for t in trees), name + ' tree jitter bounds')
    need(len({t['gain'] for t in trees}) > 3, name + ' tree gains vary independently')
    screens.append(dict(shot=name, snow_b=snow, stone_b=stone, contrast_up=contrast,
                        snow_samples=len(snow_b), stone_samples=len(stone_b), hard_lines=hard, trees=trees))
    print(f'[GRADE] shot={name} snow_b*={snow:.3f} stone_b*={stone:.3f} contrast_up={contrast:.5f} samples={len(snow_b)}/{len(stone_b)}')
    print(f'[TREES] shot={name} variants=3 heights=90/150/240 flip=true luma_jitter=0.08 position_jitter=12')
    print(f'[SEAM] shot={name} hard_lines={hard} blend_px=20 top_fog_px=40')

walk = (ROOT / 'spike/logs/climb2d_walk.log').read_text()
distance = float(re.search(r'\[PLAY\].*distance=([\d.]+)', walk)[1])
need(17.5 <= distance <= 17.9 and re.search(r'\[CLIMB_WALK6\].*bad=0', walk), 'walk6 distance/support')
timing = entry('[CLIMB2D] measured')
arrival = entry('[CLIMB2D] arrival')
report = dict(protected_files=len(protected), cliffs=cliffs, screenshots=screens, timing=timing, arrival=arrival, transition='ok', walk6_m=distance, bad=0)
(OUT / 'grade_acceptance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print(f'ACCEPT climb2d_grade total_s={timing["total_s"]} fade_s={timing["fade_s"]} transition=ok arrival_bad={arrival["bad"]} walk6_m={distance:.3f} bad=0')
