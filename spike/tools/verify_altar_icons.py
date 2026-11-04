#!/usr/bin/env python3
"""Verify final native icons, actual Unity build, new scene captures and benchmark."""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image
from altar_icons_pixel import verify, ROOT

verify()
logs=ROOT/'logs'
build=logs/'altar_icons_build.log'
text=build.read_text()
assert '[PERF_BUILD] result=Succeeded errors=0' in text
assert 'geometry_unchanged=true icon_vertices=24/24/24 tris=8880 materials=2' in text
audit=(logs/'altar_icons_audit.log').read_text()
assert re.search(r'\[ALTAR_MODEL\].*pass=True',audit)
for icon in ('left','mid','right'):
    row=next(s for s in text.splitlines() if s.startswith('[ALTAR_ICONS] icon=icon_'+icon+' '))
    assert 'filter=point no_mipmap=true aniso=1 compression=none size=512x784' in row
    checked=next(s for s in audit.splitlines() if s.startswith('[ALTAR_ICONS] icon=icon_'+icon+' '))
    assert 'grid_ok=true' in checked
inside=(logs/'altar_icons_interior.log').read_text()
passed=re.search(r'\[INT_PASS\].*',inside).group()
for key in ('light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked'):
    assert key+'=True' in passed
close=(logs/'altar_icons_closeup.log').read_text()
shot_line=re.search(r'\[ALTAR_SHOT\].*',close).group()
assert 'distance_m=1.200' in shot_line and 'isolated_inspection=False' in shot_line
for log in (inside,close):
    assert not re.search(r'Exception:|\[INT_FAIL\]|\[PLAY\].*fps_ok=false',log)
bench_path=logs/'altar_icons_bench.json'
assert bench_path.stat().st_mtime>build.stat().st_mtime
bench=json.loads(bench_path.read_text())
interior=next(x for x in bench['results'] if x['pos']=='interior')
assert 0<interior['drawcalls']<=260 and interior['fps_avg']>=200
assert '[BENCH_GATE] pass=True' in (logs/'altar_icons_bench.log').read_text()
shots=ROOT/'project/Shots'
close_path=shots/'altar_closeup.png'
frame=Image.open(close_path)
assert frame.size==(1920,1080)
small=frame.resize((480,270),Image.Resampling.LANCZOS)
small.resize((1920,1080),Image.Resampling.NEAREST).save(shots/'altar_icons_480.png')
images={}
for name in ('altar_closeup','interior_inside','altar_icons_480'):
    path=shots/(name+'.png')
    assert path.stat().st_mtime>build.stat().st_mtime, 'Stale screenshot: '+name
    assert Image.open(path).size==(1920,1080)
    images[name]=dict(path=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
report=dict(automated_pass=True,geometry_unchanged=True,tris=8880,materials=2,
            demo_pass=passed,closeup=shot_line,interior_bench=interior,images=images,
            visual_review='Review altar_icons_480.png: three distinct silhouettes; scene pixel density is a human judgment.',
            downsample='LANCZOS to480x270, NEAREST x4; no editing of source scene screenshots')
output=ROOT/'artifacts/altar/altar_icons_acceptance.json'
output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
