#!/bin/bash
# Run after accept.sh --fast: fresh exterior AB + three indoor views + STYLE5 gates.
set -eu
cd "$(dirname "$0")/../.."
bash spike/tools/run_demos.sh "-demo-enter-church"
bash spike/tools/capture_runtime.sh --style4-family
python3 - <<'PY'
import hashlib
import json
from pathlib import Path
from PIL import Image

shots = Path('spike/project/Shots')
source = shots / 'art_close.png'
with Image.open(source) as image:
    small = image.resize((480, 270), Image.Resampling.LANCZOS)
    small.save(shots / 'style_480.png')
    small.resize((1920, 1080), Image.Resampling.NEAREST).save(shots / 'style_480_upscaled.png')
evidence = dict(source=str(source), source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                downsample='LANCZOS 480x270', upscale='NEAREST 4x')
(shots / 'style5_thumbnail.json').write_text(json.dumps(evidence, indent=2)+'\n')
for name in ('interior_entry', 'interior_inside', 'interior_lookup'):
    path = shots / (name+'.png')
    assert path.is_file() and path.stat().st_size > 0, path
    print('[STYLE5_INTERIOR] file='+str(path)+' sha256='+hashlib.sha256(path.read_bytes()).hexdigest())
print('[STYLE5_THUMBNAIL] '+json.dumps(evidence))
PY
python3 spike/tools/measure_style.py
