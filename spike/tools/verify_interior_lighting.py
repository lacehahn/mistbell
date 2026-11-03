#!/usr/bin/env python3
"""Verify saved lighting shots; never change measure_shot.py or its definitions.

Surface ROIs use linear Rec.709 and bottom-left coordinates, matching the runtime
assertions. ceiling_keep preserves the existing display-referred DOME_LIGHT ROI.
Run after run_demos.sh and capture_runtime.sh; optional extra PNG paths follow.
"""
import hashlib
import subprocess
import sys
from pathlib import Path
from measure_shot import load

ROOT = Path(__file__).resolve().parents[2]
SHOTS = ROOT / 'spike/project/Shots'


def luma(buf, width, height, roi, linear):
    x, y, w, h = roi
    lut = [((c/255+.055)/1.055)**2.4 if c/255 > .04045 else c/255/12.92 for c in range(256)] if linear else [c/255 for c in range(256)]
    total = 0
    count = 0
    for py in range(round(height*y), round(height*(y+h))):
        for px in range(round(width*x), round(width*(x+w))):
            i = ((height-1-py)*width+px)*3
            total += .2126*lut[buf[i]]+.7152*lut[buf[i+1]]+.0722*lut[buf[i+2]]
            count += 1
    return total/count


def main():
    names = ['interior_entry', 'interior_inside', 'interior_lookup']
    paths = [SHOTS / (n+'.png') for n in names] + [Path(p).resolve() for p in sys.argv[1:]]
    frames = {}
    for path in dict.fromkeys(paths):
        print(f'[FRAME] file={path.relative_to(ROOT)} md5={hashlib.md5(path.read_bytes()).hexdigest()}', flush=True)
        subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(path)], check=True)
        buf, width, height = load(str(path))
        magenta = sum(buf[i]>150 and buf[i+2]>150 and buf[i+1]<100 for i in range(0,len(buf),3))
        print(f'[INT_LIGHT_PIXELS] file={path.name} magenta_px={magenta} sampling=all', flush=True)
        assert magenta == 0, path
        if path.stem in names:
            frames[path.stem] = (buf, width, height)
    inside = frames['interior_inside']
    nave = luma(*inside, (.2,.2,.6,.6), True)
    wall = (luma(*inside, (.03,.22,.14,.22), True)+luma(*inside, (.83,.22,.14,.22), True))/2
    bench = (luma(*inside, (.19,.23,.21,.065), True)+luma(*inside, (.60,.23,.21,.065), True))/2
    ceiling = luma(*frames['interior_lookup'], (.2,.2,.6,.6), False)
    print(f'[INT_LIGHT_PNG] nave_luma={nave:.4f} wall_luma={wall:.4f} bench_luma={bench:.4f} ceiling_keep={ceiling:.4f} surface_space=linear ceiling_space=display', flush=True)
    assert .16 <= nave <= .30, nave
    assert .12 <= wall <= .26, wall
    assert .10 <= bench <= .22, bench
    assert ceiling >= .28, ceiling
    print('[VERIFY_INTERIOR_LIGHTING] PASS', flush=True)


if __name__ == '__main__':
    main()
