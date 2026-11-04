#!/usr/bin/env python3
import hashlib,json,re,shutil
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1]; A=R/'artifacts/rebuild_fix_spawn'
logs={k:(R/f'logs/rebuild_spawn_{k}.log').read_text(errors='replace') for k in ['build','camera','panorama','walk','interior','reveal','bench']}
rebuild=(R/'logs/rebuild_fix_spawn_rebuild.log').read_text()
steps=re.findall(r'^\[REBUILD\] (\S+\.Run|MaterialForge.Generate) rc=(\d+) cs_errors=(\d+).*$',rebuild,re.M)
spawn=[float(v) for v in re.findall(r'\[SPAWN\] dist_to_center=([\d.]+)',logs['walk'])]
walk=[float(v) for v in re.findall(r'\[WALK_ALIGN\] max_offset=([\d.]+)',logs['walk'])]
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',logs[k],re.I)) for k in ['walk','interior','reveal']}
bench=json.loads((R/'logs/rebuild_spawn_bench.json').read_text())['results']
before=json.loads((A/'protected_before.json').read_text())['rows']; after=json.loads((A/'protected_after.json').read_text())['rows']
from collections import Counter
b=Counter((v['key'],v['value']) for v in before); a=Counter((v['key'],v['value']) for v in after)
changed=sorted({k for k,v in (a-b)+(b-a)})
hashes=json.loads((A/'protected_hashes_before.json').read_text()); source_changed=[p for p,h in hashes.items() if hashlib.sha256((R.parent/p).read_bytes()).hexdigest()!=h]
mat=(R/'project/Assets/Settings/M_FT3_Paving.mat').read_text();scale=float(re.search(r'_ScaleM: ([\d.]+)',mat)[1]); base=[float(v) for v in re.search(r'_BaseColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)',mat).groups()]
shots={}
for src,dst in [('cam_play_1080','play_view'),('art_runtime','art_runtime')]:
 p=R/'project/Shots'/f'{src}.png';size=list(Image.open(p).size);fresh=p.stat().st_mtime>(R/'logs/rebuild_spawn_build.log').stat().st_mtime
 shutil.copy2(p,A/f'{dst}.png');shots[dst]={'size':size,'fresh':fresh}
checks={'rebuild_22':len(steps)==22 and all(rc=='0' and cs=='0' for _,rc,cs in steps) and '[REBUILD] done fail=0' in rebuild,
 'player_build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
 'spawn':bool(spawn) and max(spawn)<=.15,'walk_6s':bool(walk) and max(walk)<=.25 and 'fps_ok=true' in logs['walk'],
 'demos':all(v==0 for v in bad.values()) and '[REVEAL] gate=pass' in logs['reveal'] and '[INT_PASS]' in logs['interior'],
 'outdoor_fps':len([v for v in bench if v['pos'] in ['z01','terrace'] and v['fps_avg']>=200])==2,
 'terrace_drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),
 'protected_scene':not changed,'protected_fence_assets':not source_changed and bool(hashes),
 'path_unchanged':abs(scale-2.4)<1e-6 and all(abs(v-1)<1e-6 for v in base),
 'fresh_shots':all(v['fresh'] and v['size']==[1920,1080] for v in shots.values())}
report={'pass':all(checks.values()),'checks':checks,'spawn':spawn,'walk_max':walk,'bad':bad,'bench':bench,'protected_changed':changed,'source_changed':source_changed,'scale_m':scale,'base_color':base,'shots':shots}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f'[SPAWN] dist_to_center={max(spawn) if spawn else None}')
print(f'[WALK_ALIGN] max_offset={max(walk) if walk else None}')
for k,v in bad.items():print(f'[DEMO] {k} bad={v}')
for v in bench:print(f'[BENCH] {v["pos"]} fps={v["fps_avg"]:.2f} drawcall={v["drawcalls"]}')
print(f'[PROTECTED] changed={changed} source_changed={source_changed}')
print(f'[PATH] scale_m={scale} base_color={base}')
for k,v in checks.items():print(f'[CHECK] {k}={str(v).lower()}')
raise SystemExit(0 if report['pass'] else 1)
