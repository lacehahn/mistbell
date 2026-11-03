#!/usr/bin/env python3
"""Read the real scene/capture/1080p bench evidence; leave measure_shot.py unchanged."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

import measure_shot

ROOT = Path(__file__).resolve().parents[2]
LOGS = ROOT / 'spike/logs'
SHOTS = ROOT / 'spike/project/Shots'


def fields(log, prefix):
    lines = [line for line in log.read_text().splitlines() if line.startswith(prefix)]
    if not lines:
        raise RuntimeError(f'Missing {prefix} in {log}')
    return dict(re.findall(r'(\w+)=([^ ]+)', lines[-1]))


def main():
    layout = fields(LOGS / 'fgd_scene.log', '[FGD_LAYOUT]')
    runtime = fields(Path(sys.argv[1]), '[FGD_RENDER]')
    variance = None
    art = None
    for name in ('art_runtime.png', 'art_close.png', 'art_ground.png'):
        path = SHOTS / name
        print(f'[FRAME] file={path.relative_to(ROOT)} md5={hashlib.md5(path.read_bytes()).hexdigest()}', flush=True)
        result = subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(path)],
                                capture_output=True, text=True, check=True)
        print(result.stdout.strip(), flush=True)
        if name == 'art_runtime.png':
            art = json.loads(subprocess.check_output(
                [sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(path), '--json'], text=True))
            buf, w, h = measure_shot.load(str(path))
            count = 0
            mean = m2 = 0.0
            # Exact lower-left quarter in PNG top-left coordinates; RGB luma 0..1.
            for y in range(h // 2, h):
                for x in range(w // 2):
                    i = (y * w + x) * 3
                    value = (.299 * buf[i] + .587 * buf[i + 1] + .114 * buf[i + 2]) / 255
                    count += 1
                    delta = value - mean
                    mean += delta / count
                    m2 += delta * (value - mean)
            variance = m2 / count
    bench = json.loads((LOGS / 'fgd_bench.json').read_text())['results']
    fps = min(r['fps_avg'] for r in bench)
    draws = max(r['drawcalls'] for r in bench)
    print('[FGD] ' + ' '.join(f'{key}={layout[key]}' for key in (
        'sastrugi_cov', 'rock_clusters', 'grass_patches', 'cairns'))
        + f' new_drawcalls={runtime["new_drawcalls"]} path_clear_min={layout["path_clear_min"]}'
        + f' fg_variance={variance:.7f} fps={fps:.2f}', flush=True)
    passed = (.18 <= float(layout['sastrugi_cov']) <= .30
              and int(runtime['new_drawcalls']) <= 6 and float(runtime['new_drawcalls']) >= 0
              and float(layout['path_clear_min']) >= 3 and variance >= .0015
              and len(bench) == 3 and fps >= 200 and 0 < draws <= 330
              and runtime['resolution'] == '1920x1080'
              and art['size'] == '1920x1080' and art['snow_ratio'] >= .55
              and art['white_clip_ratio'] <= .05 and art['magenta_px'] == 0
              and 12 <= int(layout['grass_patches']) <= 20
              and 3 <= int(layout['cairns']) <= 4 and 3 <= float(layout['rock_density']) <= 8)
    print(f'[FGD_GATE] pass={str(passed).lower()} max_drawcalls={draws} capture_fps={runtime["fps"]}', flush=True)
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
