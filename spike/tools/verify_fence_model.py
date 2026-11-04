#!/usr/bin/env python3
"""Independent saved-geometry, PNG, player-log and performance acceptance. No gate changes."""
import csv,hashlib,json,math,re,shutil,statistics
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/fence_model';S=R/'project/Shots'
a=json.loads((A/'assets.json').read_text())
logs={k:(R/f'logs/fence_model_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','walk','interior','reveal','bench']}
bench=json.loads((R/'logs/fence_model_bench.json').read_text())['results']
def point(p):return (p['x'],p['z'])
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def bounds(tri):return min(p[0] for p in tri),max(p[0] for p in tri),min(p[1] for p in tri),max(p[1] for p in tri)
def triangles(vertices,indices):
 for k in range(0,len(indices),3):
  t=[point(vertices[indices[k+j]]) for j in range(3)]
  if abs(cross(*t))>1e-9:yield t,bounds(t)
def intersect(t,u):
 # Separating-axis test, independent from editor edge/inside tests.
 for tri in (t,u):
  for i in range(3):
   p,q=tri[i],tri[(i+1)%3];axis=(q[1]-p[1],p[0]-q[0])
   x=[v[0]*axis[0]+v[1]*axis[1] for v in t];y=[v[0]*axis[0]+v[1]*axis[1] for v in u]
   if max(x)<min(y)-1e-8 or max(y)<min(x)-1e-8:return False
 return True
path=list(triangles(a['path_vertices'],a['path_triangles']));crossing=0
spacing=[];counts=[0,0,0];jitters=[];yaws=[]
for s in a['instances']:
 counts[s['variant']]+=1;jitters.append(s['jitter']);yaws.append(s['yaw'])
 hit=False
 for t,b in triangles(s['vertices'],s['triangles']):
  for u,c in path:
   if b[0]>c[1] or b[1]<c[0] or b[2]>c[3] or b[3]<c[2]:continue
   if intersect(t,u):hit=True;break
  if hit:break
 crossing+=int(hit)
for side in [-1,1]:
 for leg in range(4):
  segments=sorted([s for s in a['instances'] if s['side']==side and s['leg']==leg],key=lambda s:s['station'])
  for x,y in zip(segments,segments[1:]):spacing.append(math.dist(point(x['p']),point(y['p'])))
corner_gaps=[]
for side in [-1,1]:
 for leg in range(3):
  x=max([s for s in a['instances'] if s['side']==side and s['leg']==leg],key=lambda s:s['station']);y=min([s for s in a['instances'] if s['side']==side and s['leg']==leg+1],key=lambda s:s['station'])
  # Foot-prism vertices on both posts; support the rotated left-side B variant.
  vx=x['vertices'][24:48]+x['vertices'][96:120];vy=y['vertices'][24:48]+y['vertices'][96:120]
  corner_gaps.append(min(math.dist(point(p),point(q)) for p in vx for q in vy))
# Positive enclosed volume detects an inside-out mesh even if triangle budgets pass.
volumes=[]
for variant in range(3):
 s=next(s for s in a['instances'] if s['variant']==variant);v=s['vertices'];t=s['triangles'];volume=0
 for k in range(0,len(t),3):
  p,q,r=[v[t[k+j]] for j in range(3)]
  volume+=p['x']*(q['y']*r['z']-q['z']*r['y'])+p['y']*(q['z']*r['x']-q['x']*r['z'])+p['z']*(q['x']*r['y']-q['y']*r['x'])
 volumes.append(volume/6)
shots={}
for name in ['cam_play_1080','fence_closeup']:
 p=S/(name+'.png');im=Image.open(p).convert('RGB');shots[name]={'size':im.size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime>(R/'logs/fence_model_build.log').stat().st_mtime,'magenta':sum(r>150 and b>150 and g<100 for r,g,b in im.getdata())};shutil.copy2(p,A/p.name)
# Compare PNG lightness at actual ray/UV-selected sunlit painted wood pixels.
def lightness(rgb):
 rgb=[x/255 for x in rgb];rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb];y=sum(v*w for v,w in zip(rgb,[.2126729,.7151522,.072175]));return 116*y**(1/3)-16 if y>216/24389 else y*(24389/27)
close_image=Image.open(S/'fence_closeup.png').convert('RGB');groups=[[],[],[]]
for row in csv.DictReader((S/'fence_closeup_pixels.csv').open()):groups[int(row['level'])].append(lightness(close_image.getpixel((int(row['x']),int(row['y'])))))
painted_values=[{'samples':len(g),'median_L':statistics.median(g) if g else None} for g in groups]
shutil.copy2(S/'fence_closeup_pixels.csv',A/'fence_closeup_pixels.csv')
texture=Image.open(R/'project/Assets/Art/Textures/Fence_Wood_1024.png').convert('RGB');seam=sum(texture.getpixel((0,y))!=texture.getpixel((1023,y)) for y in range(1024));colors=texture.getcolors(1024*1024);shutil.copy2(R/'project/Assets/Art/Textures/Fence_Wood_1024.png',A/'Fence_Wood_1024.png')
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',s,re.I)) for k,s in logs.items() if k!='build'}
close=re.search(r'\[FENCE_CLOSEUP\] distance_m=([\d.]+)',logs['closeup']);runtime=re.search(r'\[STATIC_REMAINING\] root=FM4_Root renderers=(\d+) tris=(\d+)',logs['bench'])
checks={
 'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
 'protected':a['protected_before']==a['protected_after'],
 'fresh_logs':all((R/f'logs/fence_model_{k}.log').stat().st_mtime>(R/'logs/fence_model_build.log').stat().st_mtime for k in logs if k!='build') and (R/'logs/fence_model_bench.json').stat().st_mtime>(R/'logs/fence_model_build.log').stat().st_mtime,
 'variants':a['variants']==len(a['meshes'])==3 and all(n>0 for n in counts),
 'tri_budget':all(0<m['tris']<=600 for m in a['meshes']),
 'outward_normals':all(v>0 for v in volumes),
 'length':all(abs(m['size']['z']-2.2)<.0001 for m in a['meshes']),
 'single_rows':a['rows_per_side']==1 and set(s['side'] for s in a['instances'])=={-1,1},
 'spacing':spacing and min(spacing)>=2.05 and max(spacing)<=2.35 and max(spacing)-min(spacing)>.01,
 'corner_gaps':all(.3<=g<=.6 for g in corner_gaps),
 'jitter':min(yaws)>=-3 and max(yaws)<=3 and min(jitters)>=-.02 and max(jitters)<=.02 and max(jitters)-min(jitters)>.02,
 'ratio':counts==a['counts'] and all(abs(counts[i]/len(a['instances'])-r)<.01 for i,r in enumerate([.8,.1,.1])),
 'crossing':crossing==a['crossing']==0,
 'batches':0<a['batches']<=6 and runtime is not None and int(runtime[1])==a['batches'] and int(runtime[2])==a['tris'],
 'texture':texture.size==(1024,1024) and a['texture_width']==a['texture_height']==1024 and len(colors)==4 and {tuple(c) for _,c in colors}=={(56,44,34),(74,59,46),(92,74,56),(126,121,106)} and seam==0,
 'wood_grain_visible':all(v['samples']>=10 for v in painted_values) and painted_values[2]['median_L']-painted_values[0]['median_L']>=3,
 'closeup':close is not None and float(close[1])<=3,
 'screenshots':all(v['size']==(1920,1080) and v['fresh'] and v['magenta']==0 for v in shots.values()),
 'walk':'[PLAY]' in logs['walk'] and 'fps_ok=true' in logs['walk'] and bool(re.search(r'distance=(1[5-9]|[2-9]\d)',logs['walk'])) and 'raycast_misses=0' in logs['walk'],
 'interior':bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True',logs['interior'])),
 'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
 'bad_zero':all(v==0 for v in bad.values()),
 'camera':len(re.findall(r'\[CAM_TEST\].*bad=0',logs['camera']))==2 and 'dist=11.50 h=2.10 look=1.20 fov=48.00' in logs['camera'],
 'outdoor_fps':len([v for v in bench if v['pos'] in ['z01','terrace']])==2 and all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']),
 'terrace_drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),
}
report={'pass':all(checks.values()),'checks':checks,'bad':bad,'segments':a['segments'],'tris':a['tris'],'batches':a['batches'],'variants':a['meshes'],'counts':counts,'spacing_min':min(spacing),'spacing_max':max(spacing),'crossing':crossing,'corner_gaps':corner_gaps,'jitter_min':min(jitters),'jitter_max':max(jitters),'yaw_min':min(yaws),'yaw_max':max(yaws),'painted_values_PNG':painted_values,'mesh_signed_volumes':volumes,'texture_colors':colors,'seam_mismatches':seam,'closeup_distance_m':float(close[1]) if close else None,'protected_before':a['protected_before'],'protected_after':a['protected_after'],'bench':bench,'shots':shots}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f"[FENCE_MESH] segments={a['segments']} tris={a['tris']} batches={a['batches']} spacing={min(spacing):.6f}..{max(spacing):.6f} crossing={crossing} variants={a['variants']} counts={counts}")
for k in ['walk','interior','reveal']:print(f'[DEMO] {k} bad={bad[k]}')
for v in bench:print(f"[BENCH] pos={v['pos']} fps_avg={v['fps_avg']:.2f} fps_p1={v['fps_p1']:.2f} drawcall={v['drawcalls']}")
for k,v in checks.items():print(f'[CHECK] {k}={str(bool(v)).lower()}')
print('pass='+str(report['pass']).lower());raise SystemExit(0 if report['pass'] else 1)
