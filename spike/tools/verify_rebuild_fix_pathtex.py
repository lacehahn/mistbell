#!/usr/bin/env python3
"""Measure actual rendered PNG path pixels, protected scene snapshots and runtime logs."""
import csv,hashlib,json,math,re,shutil
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/rebuild_fix_pathtex';S=R/'project/Shots'
logs={k:(R/f'logs/rebuild_fix_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','panorama','walk','interior','reveal','bench']}
rebuild=(R/'logs/rebuild_fix_pathtex_rebuild.log').read_text()
rows=lambda n:json.loads((A/f'protected_{n}.json').read_text())['rows']
before,after=rows('before'),rows('after')
# Compare multisets: repeated interior mesh names are legal.
protected=before==after
changed=[]
from collections import Counter
b=Counter((v['key'],v['value']) for v in before);a=Counter((v['key'],v['value']) for v in after)
changed=sorted({k for k,v in (a-b)+(b-a)})
source_hashes=json.loads((A/'before_hashes.json').read_text());source_changed=[p for p,h in source_hashes.items() if hashlib.sha256((R.parent/p).read_bytes()).hexdigest()!=h]
def L(rgb):
 c=[v/255 for v in rgb[:3]];r,g,b=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in c]
 y=.2126729*r+.7151522*g+.0721750*b
 return 116*y**(1/3)-16 if y>216/24389 else 24389/27*y
im=Image.open(S/'path_closeup.png').convert('RGB');points=[(int(v['x']),int(v['y'])) for v in csv.DictReader((S/'path_closeup_tone_pixels.csv').open()) if v['kind']=='path']
values=[L(im.getpixel(p)) for p in points];luma=sum(values)/len(values) if values else 0
mat=(R/'project/Assets/Settings/M_FT3_Paving.mat').read_text();scale=float(re.search(r'_ScaleM: ([\d.]+)',mat)[1]);base=[float(v) for v in re.search(r'_BaseColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)',mat).groups()]
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',s,re.I)) for k,s in logs.items() if k!='build'}
spawn=[float(v) for v in re.findall(r'\[SPAWN\] dist_to_path_center=([\d.]+)',logs['walk'])]
walk=[float(v) for v in re.findall(r'\[PATH_WALK\] max_center_error=([\d.]+)',logs['walk'])]
bench=json.loads((R/'logs/rebuild_fix_bench.json').read_text())['results']
shots={}
for src,dst in [('cam_play_1080','play_view'),('art_runtime','art_runtime'),('path_closeup','path_closeup')]:
 p=S/(src+'.png');size=list(Image.open(p).size);fresh=p.stat().st_mtime>(R/'logs/rebuild_fix_build.log').stat().st_mtime
 shutil.copy2(p,A/(dst+'.png'));shots[dst]={'size':size,'fresh':fresh,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
shutil.copy2(S/'path_closeup_tone_pixels.csv',A/'path_closeup_tone_pixels.csv')
checks={'rebuild_22':'[REBUILD] 22/22 pass' in rebuild and '[REBUILD] done fail=0' in rebuild,'player_build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],'protected_scene':protected,'protected_sources':not source_changed,'scale_1_35':abs(scale-1.35)<1e-6,'base_gain_6pct':all(abs(v-1.06)<1e-5 for v in base),'near_path_luma':len(values)>100 and 58<=luma<=62,'spawn_center':bool(spawn) and max(spawn)<=.15,'walk_center':bool(walk) and max(walk)<=.3,'demos':all(bad[k]==0 for k in ['walk','interior','reveal']) and '[REVEAL] gate=pass' in logs['reveal'] and '[INT_PASS]' in logs['interior'] and 'fps_ok=true' in logs['walk'],'outdoor_fps':all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']) and len([v for v in bench if v['pos'] in ['z01','terrace']])==2,'terrace_drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),'fresh_1080_shots':all(v['fresh'] and v['size']==[1920,1080] for v in shots.values())}
report={'pass':all(checks.values()),'checks':checks,'path':{'scale_m':scale,'base_color':base,'luma_L_star':luma,'samples':len(values),'method':'mean CIELAB L* D65 from fresh unprocessed closeup PNG, geometry-selected eroded road interior pixels; snow edge excluded'},'spawn_distance':spawn,'walk_center_max':walk,'bad':bad,'bench':bench,'protected_rows':len(before),'protected_changed':changed,'protected_source_changed':source_changed,'shots':shots}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f'[PATH_TEX2] scale_m={scale:.2f} luma={luma:.6f} samples={len(values)}')
print(f'[SPAWN] dist_to_path_center={max(spawn) if spawn else None}')
print(f'[PATH_WALK] max_center_error={max(walk) if walk else None}')
for k in ['walk','interior','reveal']:print(f'[DEMO] {k} bad={bad[k]}')
for v in bench:print(f'[BENCH] {v["pos"]} fps={v["fps_avg"]:.2f} p1={v["fps_p1"]:.2f} drawcall={v["drawcalls"]}')
print(f'[PROTECTED] rows={len(before)} changed={changed} source_changed={source_changed}')
for k,v in checks.items():print(f'[CHECK] {k}={str(v).lower()}')
print(f'pass={str(report["pass"]).lower()}');raise SystemExit(0 if report['pass'] else 1)
