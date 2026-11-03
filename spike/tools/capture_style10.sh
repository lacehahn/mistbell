#!/bin/bash
set -eu
cd "$(dirname "$0")/../.."
START=$(date +%s)
bash spike/tools/capture_runtime.sh
bash spike/tools/run_demos.sh "-demo-enter-church"
python3 - "$START" <<'PY'
import hashlib,json,sys
from pathlib import Path
from PIL import Image
sys.path.insert(0,'spike/tools')
from forge_style10 import ROOT,NAMES,sha
shots=Path('spike/project/Shots')
names=['art_close.png','art_runtime.png','interior_entry.png','interior_inside.png','interior_lookup.png']
for name in names:assert (shots/name).stat().st_mtime>=int(sys.argv[1]),'stale '+name
im=Image.open(shots/'art_close.png');small=im.resize((480,270),Image.Resampling.LANCZOS);small.save(shots/'style_480.png');small.resize((1920,1080),Image.Resampling.NEAREST).save(shots/'style_480_upscaled.png')
names+=['style_480.png','style_480_upscaled.png']
data=dict(textures={n:sha(ROOT/(n+'_Albedo.png')) for n in NAMES},shots={n:sha(shots/n) for n in names})
(shots/'style10_capture.json').write_text(json.dumps(data,indent=2)+'\n')
print('[STYLE10_CAPTURE] fresh screenshots; LANCZOS 480x270 -> NEAREST x4; texture and image hashes saved')
PY
