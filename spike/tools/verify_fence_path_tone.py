#!/usr/bin/env python3
"""Independent PNG CIELAB D65 samples and layout/demo/performance gates for FT3."""
import csv,hashlib,json,math,re,shutil
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/fence_path_tone';S=R/'project/Shots'
assets=json.loads((A/'assets.json').read_text())
logs={k:(R/f'logs/fence_path_tone_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','walk','interior','reveal','bench']}
bench=json.loads((R/'logs/fence_path_tone_bench.json').read_text())['results']
def lab(c):
 v=[x/255 for x in c[:3]];r,g,b=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in v]
 xyz=[(.4124564*r+.3575761*g+.1804375*b)/.95047,.2126729*r+.7151522*g+.072175*b,(.0193339*r+.119192*g+.9503041*b)/1.08883]
 f=[x**(1/3) if x>216/24389 else (24389/27*x+16)/116 for x in xyz]
 return 116*f[1]-16,500*(f[0]-f[1]),200*(f[1]-f[2])
def metric(im,points):
 values=[lab(im.getpixel(p)) for p in points];n=len(values)
 if not n:return {'samples':0,'L':None,'b':None,'L_max':None}
 return {'samples':n,'L':sum(v[0] for v in values)/n,'b':sum(v[2] for v in values)/n,'L_max':max(v[0] for v in values)}
shots={};pixels={};before={}
for name in ['cam_play_1080','path_closeup']:
 p=S/(name+'.png');im=Image.open(p).convert('RGB');rows=list(csv.DictReader((S/(name+'_tone_pixels.csv')).open()))
 groups={kind:[(int(v['x']),int(v['y'])) for v in rows if v['kind']==kind] for kind in ['path','sunlit_post','edge']}
 pixels[name]={k:metric(im,points) for k,points in groups.items()}
 old=Image.open(A/('before_'+name+'.png')).convert('RGB');before[name]={k:metric(old,points) for k,points in groups.items()}
 shots[name]={'size':im.size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime>(R/'logs/fence_path_tone_build.log').stat().st_mtime,'magenta':sum(r>150 and b>150 and g<100 for r,g,b in im.getdata())}
 shutil.copy2(p,A/p.name);shutil.copy2(S/(name+'_tone_pixels.csv'),A/(name+'_tone_pixels.csv'))
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',s,re.I)) for k,s in logs.items() if k!='build'}
# Independently recompute distances from the persisted post transforms.
spacings=[];spatial=[]
for side in [-1,1]:
 posts=sorted([p for p in assets['posts'] if p['side']==side],key=lambda p:p['p']['z'])
 for a,b in zip(posts,posts[1:]):
  d=[b['p'][c]-a['p'][c] for c in ['x','y','z']];spacings.append(math.hypot(d[0],d[2]));spatial.append(math.sqrt(sum(v*v for v in d)))
p=pixels['path_closeup']['path'];f=pixels['path_closeup']['sunlit_post']
checks={
 'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
 'fresh_logs':all((R/f'logs/fence_path_tone_{k}.log').stat().st_mtime>(R/'logs/fence_path_tone_build.log').stat().st_mtime for k in logs if k!='build') and (R/'logs/fence_path_tone_bench.json').stat().st_mtime>(R/'logs/fence_path_tone_build.log').stat().st_mtime,
 'protected':assets['protected_before']==assets['protected_after']==json.loads((A/'initial_assets.json').read_text())['protected_before'],
 'spacing':spacings and min(spacings)>=2.1 and max(spacings)<=2.3 and max(spatial)<=2.3,
 'single_rows':assets['rows_per_side']==1 and assets['count']==len(assets['posts']) and assets['renderers']==5,
 'post_size':all(all(abs(p['size'][c]-v)<1e-6 for c,v in [('x',.08),('y',1),('z',.08)]) for p in assets['posts']),
 'wood_material':assets['post_color']=='#4A3B2E' and abs(assets['roughness']-.85)<1e-6 and abs(assets['snow_depth']-.04)<1e-6,
 'wood_lit':f['samples']>=10 and f['L_max']<=45 and pixels['cam_play_1080']['sunlit_post']['L']<=45,
 'crossing':assets['crossing']==0,
 'path_L':p['samples']>=100 and 52<=p['L']<=58,
 'path_b':p['samples']>=100 and 2<=p['b']<=3,
 'path_gain':1.08<=p['L']/before['path_closeup']['path']['L']<=1.10,
 'edge':assets['edge_ok'] and .06<=assets['edge_width']<=.08 and assets['edge_color']=='#E8EFF6',
 'ridges':2<=len(assets['ridges'])<=3 and all(.15<=v['height']<=.3 and 3<=v['length']<=6 for v in assets['ridges']),
 'walk':'[PLAY]' in logs['walk'] and 'fps_ok=true' in logs['walk'] and bool(re.search(r'distance=(1[5-9]|[2-9]\d)',logs['walk'])) and 'raycast_misses=0' in logs['walk'],
 'interior':bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True',logs['interior'])),
 'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
 'bad_zero':all(n==0 for n in bad.values()),
 'camera':len(re.findall(r'\[CAM_TEST\].*bad=0',logs['camera']))==2 and 'dist=11.50 h=2.10 look=1.20 fov=48.00' in logs['camera'],
 'outdoor_fps':len([v for v in bench if v['pos'] in ['z01','terrace']])==2 and all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']),
 'terrace_drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),
 'screenshots':all(v['size']==(1920,1080) and v['fresh'] and v['magenta']==0 for v in shots.values()),
}
report={'pass':all(checks.values()),'checks':checks,'bad':bad,'pixels':pixels,'before_same_pixels':before,'spacing_min':min(spacings),'spacing_max':max(spacings),'spacing_3d_max':max(spatial),'assets':assets,'bench':bench,'shots':shots}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f"[FENCE3] spacing={min(spacings):.6f}..{max(spacings):.6f} count={assets['count']} crossing={assets['crossing']} lit_L_mean={f['L']} lit_L_max={f['L_max']} samples={f['samples']}")
print(f"[PATH3] luma={p['L']} b_star={p['b']} edge_ok={str(checks['edge']).lower()} samples={p['samples']} metric=mean_geometry_mask_PNG_CIELAB_D65")
print(f"[SNOW3] ridges={len(assets['ridges'])}")
for k in ['walk','interior','reveal']:print(f"[DEMO] {k} bad={bad[k]}")
for v in bench:print(f"[BENCH] pos={v['pos']} fps_avg={v['fps_avg']:.2f} fps_p1={v['fps_p1']:.2f} drawcall={v['drawcalls']}")
for k,v in checks.items():print(f"[CHECK] {k}={str(bool(v)).lower()}")
print('pass='+str(report['pass']).lower());raise SystemExit(0 if report['pass'] else 1)
