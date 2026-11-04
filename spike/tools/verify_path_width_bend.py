#!/usr/bin/env python3
"""Recompute acceptance from exported mesh positions, fresh player logs and PNGs."""
import hashlib,json,math,re,shutil
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1]; A=R/'artifacts/path_width_bend'; S=R/'project/Shots'
a=json.loads((A/'assets.json').read_text()); logs={k:(R/f'logs/path_width_bend_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','walk','interior','reveal','bench','trail']}
def xy(p):return p['x'],p.get('z',p['y'])
def sub(p,q):return p[0]-q[0],p[1]-q[1]
def cross(p,q):return p[0]*q[1]-p[1]*q[0]
def distance(p,q):return math.dist(p,q)
def segment_distance(p,a,b):
 d=sub(b,a);s=sum(x*y for x,y in zip(sub(p,a),d))/sum(x*x for x in d);s=max(0,min(1,s));return distance(p,(a[0]+s*d[0],a[1]+s*d[1]))
def overlaps(t,u):
 # Strict area overlap; shared edges and projected vertical wall faces are excluded.
 if abs(cross(sub(t[1],t[0]),sub(t[2],t[0])))<1e-8 or abs(cross(sub(u[1],u[0]),sub(u[2],u[0])))<1e-8:return False
 for tri in (t,u):
  for p,q in zip(tri,tri[1:]+tri[:1]):
   d=sub(q,p);axis=(-d[1],d[0]);x=[sum(c*v for c,v in zip(axis,p)) for p in t];y=[sum(c*v for c,v in zip(axis,p)) for p in u]
   if max(x)<=min(y)+1e-8 or max(y)<=min(x)+1e-8:return False
 return True
def triangles(vertices,indices):
 for j in range(0,len(indices),3):yield [xy(vertices[indices[j+k]]) for k in range(3)]
c=[xy(p) for p in a['centers']];v=[xy(p) for p in a['path_vertices']];radii=[];angles=[]
for p,q,r in zip(c,c[1:],c[2:]):
 d=sub(q,p);e=sub(r,q);z=abs(cross(d,e));l=distance(p,q);m=distance(q,r)
 if z>1e-7:radii.append(l*m*distance(p,r)/(2*z))
 angles.append(math.degrees(math.acos(max(-1,min(1,sum(x*y for x,y in zip(d,e))/(l*m))))))
widths=[distance(v[i],v[i+5]) for i in range(0,len(v),6)];edges=[distance(v[i],v[i+2]) for i in range(0,len(v),6)]
strips=[]
for i in range(len(c)-1):
 p,q,r,s=v[i*6],v[i*6+5],v[(i+1)*6],v[(i+1)*6+5];strips.append([[p,r,q],[q,r,s]])
self_hits=sum(any(overlaps(t,u) for t in strips[i] for u in strips[j]) for i in range(len(strips)) for j in range(i+2,len(strips)))
wall_hits=[]
for wall in a['walls']:
 if any(overlaps(t,u) for strip in strips for t in strip for u in triangles(wall['vertices'],wall['triangles'])):wall_hits.append(wall['name'])
fence_hits=0;spacing=[];offsets=[]
for f in a['fences']:
 p=xy(f['p']);offsets.append(min(segment_distance(p,x,y) for x,y in zip(c,c[1:])))
 if any(overlaps(t,u) for t in triangles(f['vertices'],f['triangles']) for strip in strips for u in strip):fence_hits+=1
for side in [-1,1]:
 fs=[xy(f['p']) for f in a['fences'] if f['side']==side];spacing.extend(distance(p,q) for p,q in zip(fs,fs[1:]))
foot_offsets=[min(segment_distance(xy(p),x,y) for x,y in zip(c,c[1:])) for p in a['footprints']]
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',logs[k],re.I)) for k in ['camera','closeup','walk','interior','reveal','bench','trail']}
bench=json.loads((R/'logs/path_width_bend_bench.json').read_text())['results'];walk=re.search(r'\[PLAY\].*distance=([\d.]+)',logs['walk']);walk_distance=float(walk[1]) if walk else 0
shots={}
for name in ['cam_play_1080','path_closeup']:
 p=S/(name+'.png');im=Image.open(p).convert('RGB');shots[name]={'size':im.size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime>(R/'logs/path_width_bend_build.log').stat().st_mtime,'magenta':sum(r>150 and b>150 and g<100 for r,g,b in im.getdata())};shutil.copy2(p,A/p.name)
checks={
 'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
 'protected':a['protected_before']==a['protected_after'],
 'width':min(widths)>=2 and max(widths)<=2.2,
 'edge':all(abs(e-.07)<1e-4 for e in edges),
 'scale':abs(a['scale_m']-2.4)<1e-5,
 'samples':all(.98<=distance(p,q)<=1.0001 for p,q in zip(c,c[1:])),
 'radius':min(radii)>=6,'angle':max(angles)<=12,'self_intersect':self_hits==0,'walls':not wall_hits,
 'height':a['climb_before']==a['climb_after'] and a['endpoint_before']==a['endpoint_after'],
 'fence_spacing':min(spacing)>=2.05 and max(spacing)<=2.35,
 'fence_edge_gap':all(abs(d-1.4)<.01 for d in offsets),'fence_crossing':fence_hits==0,'fence_batch':a['fence_batches']==1,
 'footprints':len(foot_offsets)==64 and max(foot_offsets)+.25<.98,
 'walk':14.63<=walk_distance<=16.17 and 'raycast_misses=0' in logs['walk'] and 'fps_ok=true' in logs['walk'],
 'interior':bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True',logs['interior'])),
 'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
 'trail':bool(re.search(r'\[TRAIL_SHOT\] spawns=(?:[89]|[1-9]\d+) walk_pass=true',logs['trail'])),
 'bad_zero':all(n==0 for n in bad.values()),
 'outdoor_fps':len([b for b in bench if b['pos'] in ['z01','terrace']])==2 and all(b['fps_avg']>=200 for b in bench if b['pos'] in ['z01','terrace']),
 'drawcall':any(b['pos']=='terrace' and 0<b['drawcalls']<=330 for b in bench),
 'screenshots':all(s['size']==(1920,1080) and s['fresh'] and s['magenta']==0 for s in shots.values()),
 'fresh_logs':all((R/f'logs/path_width_bend_{k}.log').stat().st_mtime>(R/'logs/path_width_bend_build.log').stat().st_mtime for k in logs if k!='build')}
report={'pass':all(checks.values()),'checks':checks,'width_min':min(widths),'width_max':max(widths),'min_radius':min(radii),'max_angle':max(angles),'samples':len(c),'self_intersections':self_hits,'wall_overlaps':wall_hits,'fence_crossings':fence_hits,'fence_spacing':[min(spacing),max(spacing)],'fence_offset':[min(offsets),max(offsets)],'footprint_max_offset':max(foot_offsets),'walk_distance':walk_distance,'bad':bad,'bench':bench,'shots':shots,'climb':a['climb_after'],'endpoint':a['endpoint_after']}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f"[PATH_W] width={min(widths):.6f}..{max(widths):.6f} min_turn_radius={min(radii):.6f} samples={len(c)} self_intersect={str(self_hits>0).lower()}")
print(f"[PATH_SMOOTH] max_angle_step={max(angles):.6f} wall_overlaps={len(wall_hits)} fence_crossings={fence_hits}")
for k,n in bad.items():print(f'[DEMO] {k} bad={n}')
print(f'[WALK] distance={walk_distance:.3f}')
for b in bench:print(f"[BENCH] {b['pos']} fps={b['fps_avg']:.2f} drawcall={b['drawcalls']}")
for k,b in checks.items():print(f'[CHECK] {k}={str(b).lower()}')
print(f"pass={str(report['pass']).lower()}");raise SystemExit(0 if report['pass'] else 1)
