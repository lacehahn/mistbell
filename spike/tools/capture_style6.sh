#!/bin/bash
# Fresh standalone interior, exterior frames and exact thumbnail chain; no installs.
set -eu
cd "$(dirname "$0")/../.."
START=$(date +%s)
bash spike/tools/run_demos.sh "-demo-enter-church"
bash spike/tools/capture_runtime.sh
python3 - "$START" <<'PY'
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

shots=Path('spike/project/Shots')
start=int(sys.argv[1]); evidence={}
for name in ('art_close','art_runtime','interior_entry','interior_inside','interior_lookup'):
    path=shots/(name+'.png')
    assert path.stat().st_mtime>=start, 'Stale screenshot: '+str(path)
    evidence[name]=dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(),mtime=path.stat().st_mtime)
with Image.open(shots/'art_close.png') as source:
    small=source.resize((480,270),Image.Resampling.LANCZOS)
    small.save(shots/'style_480.png')
    up=small.resize((1920,1080),Image.Resampling.NEAREST)
    up.save(shots/'style_480_upscaled.png')
    # Reopen the encoded result and verify the complete pixel chain.
    with Image.open(shots/'style_480_upscaled.png') as actual:
        assert actual.tobytes()==up.tobytes()
evidence['thumbnail']=dict(source='art_close.png',downsample='LANCZOS 480x270',upscale='NEAREST 4x',sha256=hashlib.sha256((shots/'style_480_upscaled.png').read_bytes()).hexdigest())
(shots/'style6_capture.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('[STYLE6_CAPTURE] '+json.dumps(evidence),flush=True)
PY
python3 spike/tools/measure_style6.py
