#!/usr/bin/env python3
"""Validate saved scene report, original importer settings, fresh player logs and frames."""
import hashlib,json,re,shutil
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/path_texture';S=R/'project/Shots'
logs={k:(R/f'logs/path_texture_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','walk','interior','reveal','bench']}
assets=json.loads((A/'assets.json').read_text());bench=json.loads((R/'logs/path_texture_bench.json').read_text())['results']
def lines(k,tag):return re.findall(r'^\['+tag+r'\].*$',logs[k],re.M)
def num(s,k):return float(re.search(r'\b'+re.escape(k)+r'=([-\d.]+)',s)[1])
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error',v,re.I)) for k,v in logs.items() if k!='build'}
checks={
 'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
 'protected':assets['protected_before']==assets['protected_after'],
 'textures':'[PATH_TEX] a=path_stone_a snow=path_stone_snow scale_m=2.4 blend_band=8 mipmap=true aniso=4' in logs['build'],
 'white':assets['edge_removed'] and assets['center_gap_after']==0 and '[PATH_WHITE]' in logs['build'],
 'altitude':abs(assets['snow_line']-(assets['terrace_bottom']-10))<1e-5 and assets['blend_band']==8,
 'treads':assets['treads']==9 and assets['top_triangles']>0,
 'camera':len(lines('camera','CAM_TEST'))==2 and all(num(s,'bad')==0 for s in lines('camera','CAM_TEST')) and all(num(s,'dist')==11.5 and num(s,'h')==2.1 and num(s,'look')==1.2 and num(s,'fov')==48 for s in lines('camera','CAM_FRAME')),
 'closeup':bool(lines('closeup','PATH_CLOSEUP')) and num(lines('closeup','PATH_CLOSEUP')[0],'distance_m')==3,
 'walk':bool(lines('walk','PLAY')) and 'fps_ok=true' in logs['walk'] and num(logs['walk'],'distance')>=15 and num(logs['walk'],'ground_error_max')<=.02 and num(logs['walk'],'raycast_misses')==0,
 'interior':bool(lines('interior','INT_PASS')) and all(k+'=True' in lines('interior','INT_PASS')[-1] for k in ['light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked']),
 'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
 'outdoor_fps':len([b for b in bench if b['pos'] in ['z01','terrace']])==2 and all(b['fps_avg']>=200 for b in bench if b['pos'] in ['z01','terrace']),
 'terrace_drawcall':any(b['pos']=='terrace' and 0<b['drawcalls']<=330 for b in bench),
 'bad_zero':all(v==0 for v in bad.values())}
for n in ['path_stone_a.jpg','path_stone_snow.jpg','path_stone_steps_tread.png']:
 p=R/'project/Assets/Art/Textures/Path'/n;meta=Path(str(p)+'.meta').read_text()
 checks['import_'+n]=all(re.search(pat,meta) for pat in [r'enableMipMap: 1',r'sRGBTexture: 1',r'filterMode: 1',r'aniso: 4',r'wrapU: 0',r'wrapV: '+('1' if n.endswith('.png') else '0')])
shots={}
for name in ['path_closeup','cam_play_1080','path_lower','path_upper','path_steps']:
 p=S/(name+'.png');im=Image.open(p).convert('RGB');magenta=sum(r>150 and b>150 and g<100 for r,g,b in im.getdata())
 checks[name]=im.size==(1920,1080) and magenta==0 and p.stat().st_mtime>(R/'logs/path_texture_build.log').stat().st_mtime
 shots[name]={'size':im.size,'magenta_px':magenta,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()};shutil.copy2(p,A/p.name)
report={'pass':all(checks.values()),'checks':checks,'bad':bad,'assets':assets,'bench':bench,'shots':shots}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
for tag in ['PATH_TEX','PATH_WHITE','PATH_ALTITUDE']:
 for line in lines('build',tag):print(line)
for k in ['camera','closeup','walk','interior','reveal']:
 for tag in ['CAM_TEST','PATH_CLOSEUP','PLAY','INT_PASS','REVEAL']:
  for line in lines(k,tag):print(line)
for b in bench:print(f"[BENCH] pos={b['pos']} fps_avg={b['fps_avg']:.2f} fps_p1={b['fps_p1']:.2f} drawcall={b['drawcalls']}")
for k,v in bad.items():print(f'{k} bad={v}')
for k,v in checks.items():print(f'[CHECK] {k}={str(v).lower()}')
print('pass='+str(report['pass']).lower());raise SystemExit(0 if report['pass'] else 1)
