#!/usr/bin/env python3
"""Recompute acceptance from exported geometry, fresh PNGs and unchanged player assertions."""
import csv,hashlib,json,math,re,shutil,statistics
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/fence_refine';S=R/'project/Shots'
a=json.loads((A/'assets.json').read_text())
logs={k:(R/f'logs/fence_refine_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','walk','interior','reveal','bench']}
bench=json.loads((R/'logs/fence_refine_bench.json').read_text())['results']
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

def xyz(v):return tuple(v[k] for k in ['x','y','z'])
def sub(p,q):return tuple(x-y for x,y in zip(p,q))
def dot(p,q):return sum(x*y for x,y in zip(p,q))
def norm(p):return math.sqrt(dot(p,p))
def unit(p):return tuple(v/norm(p) for v in p)
def postlocal(v,p):
 d=sub(xyz(v),xyz(p['foot']));y=d[1];d=sub(d,tuple(x*math.tan(math.radians(p['lean']))*y for x in xyz(p['outward'])));f=xyz(p['forward']);return (d[0]*f[2]-d[2]*f[0],y,dot(d,f))
def bbox(v):return [max(p[i] for p in v)-min(p[i] for p in v) for i in range(3)]
def area2(p,q,r):return abs((q['x']-p['x'])*(r['y']-p['y'])-(q['y']-p['y'])*(r['x']-p['x']))
path=list(triangles(a['path_vertices'],a['path_triangles']));posts=a['posts'];spans=a['spans'];pieces=[p for s in spans for p in s['pieces']]+a['terminals']
counts=[sum(s['variant']==i for s in spans) for i in range(3)];crossings=0;embed=[];gap=[];snow_dims=[];uv_bad=0;long_bad=0;volumes=[];rail_counts=[];cap_counts={};post_counts={};spacing=[];lean_actual=[];outward=[];snow_rail_dims=[];column_dims=[];bevel_bad=0;axis_bad=0;join_outside=0
for p in pieces:
 vs=p['vertices'];ts=p['triangles'];uv=p['uv'];volume=0
 for i in range(0,len(ts),3):
  x,y,z=[xyz(vs[ts[i+j]]) for j in range(3)];volume+=dot(x,(y[1]*z[2]-y[2]*z[1],y[2]*z[0]-y[0]*z[2],y[0]*z[1]-y[1]*z[0]))/6
 volumes.append(volume)
 if not p['kind'].endswith('snow'):
  uv_bad+=sum(area2(*(uv[ts[k+j]] for j in range(3)))<1e-7 for k in range(0,len(ts),3))
  # Four side faces: V must vary along the long edge, U along its cross section.
  faces=[2,3,4,5] if p['kind']=='column' else [0,1,4,5]
  for face in faces:
   inds=list(range(face*4,face*4+4));pairs=[(i,j) for i in inds for j in inds if j>i and abs(uv[i]['x']-uv[j]['x'])<1e-5 and abs(uv[i]['y']-uv[j]['y'])>.5]
   if len(pairs)!=2:long_bad+=1
   axis=unit(sub(xyz(p['end']),xyz(p['start']))) if p['kind']=='rail' else unit(tuple(sum(vs[k][c] for k in range(4,8))/4-sum(vs[k][c] for k in range(4))/4 for c in ['x','y','z']))
   for i,j in pairs:
    if abs(dot(unit(sub(xyz(vs[j]),xyz(vs[i]))),axis))<.999:axis_bad+=1
 if p['kind']=='column':
  post_counts[p['post']]=post_counts.get(p['post'],0)+1
  local=[postlocal(v,posts[p['post']]) for v in vs];column_dims.append(bbox(local))
  delta=tuple(sum(vs[k][c] for k in range(4,8))/4-sum(vs[k][c] for k in range(4))/4 for c in ['x','y','z'])
  if posts[p['post']]['variant']==1:
   lean_actual.append(math.degrees(math.atan2(math.hypot(delta[0],delta[2]),delta[1])));outward.append(dot(unit((delta[0],0,delta[2])),xyz(posts[p['post']]['outward'])))
 if p['kind']=='rail_snow':
  start=xyz(p['start']);end=xyz(p['end']);d=unit(sub(end,start));planar=math.hypot(d[0],d[2]);x=(d[2]/planar,0,-d[0]/planar)
  local=[]
  for v in vs:
   q=sub(xyz(v),start);z=(q[0]*d[0]+q[2]*d[2])/(planar*planar);local.append((dot(q,x),q[1]-d[1]*z,z))
  snow_rail_dims.append(bbox(local))
 if p['kind']=='column_snow':
  cap_counts[p['post']]=cap_counts.get(p['post'],0)+1;local=[postlocal(v,posts[p['post']]) for v in vs];snow_dims.append(bbox(local));bevel_bad+=sum(abs(abs(v[0])-.04)>.0001 and abs(abs(v[0])-.05)>.0001 or abs(abs(v[2])-.04)>.0001 and abs(abs(v[2])-.05)>.0001 for v in local)
 hit=False
 for t,b in triangles(vs,ts):
  for u,c in path:
   if b[0]>c[1] or b[1]<c[0] or b[2]>c[3] or b[3]<c[2]:continue
   if intersect(t,u):hit=True;break
  if hit:break
 crossings+=int(hit)
for s in spans:
 ps=s['pieces'];rails=[p for p in ps if p['kind']=='rail'];snows=[p for p in ps if p['kind']=='rail_snow'];rail_counts.append(len(rails));pa,pb=posts[s['a']],posts[s['b']];spacing.append(math.dist(point(pa['foot']),point(pb['foot'])))
 for rail in rails:
  for vertex in rail['vertices']:
   owner=pa if math.dist(xyz(vertex),xyz(rail['start']))<math.dist(xyz(vertex),xyz(rail['end'])) else pb
   q=postlocal(vertex,owner);join_outside+=int(max(abs(q[0]),abs(q[2]))>.0451)
  for end,p,other in [(rail['start'],pa,rail['end']),(rail['end'],pb,rail['start'])]:
   q=postlocal(end,p);q2=postlocal(other,p);direction=unit(sub(q2,q));planar=math.hypot(direction[0],direction[2]);dist=min((.045*(1 if direction[k]>0 else -1)-q[k])/direction[k] for k in [0,2] if abs(direction[k])>1e-7);embed.append(dist*planar);gap.append(max(0,-dist*planar))
# Recompute shared topology from indices, with one terminal column per side.
shared=all(y['b']==z['a'] for side in [-1,1] for y,z in zip([s for s in spans if s['side']==side],[s for s in spans if s['side']==side][1:]))
shots={}
for name in ['cam_play_1080','fence_closeup']:
 p=S/(name+'.png');im=Image.open(p).convert('RGB');shots[name]={'size':im.size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime>(R/'logs/fence_refine_build.log').stat().st_mtime,'magenta':sum(r>150 and b>150 and g<100 for r,g,b in im.getdata())};shutil.copy2(p,A/p.name)
def lightness(rgb):
 rgb=[x/255 for x in rgb];rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb];y=sum(v*w for v,w in zip(rgb,[.2126729,.7151522,.072175]));return 116*y**(1/3)-16 if y>216/24389 else y*(24389/27)
im=Image.open(S/'fence_closeup.png').convert('RGB');groups=[[],[],[]]
for row in csv.DictReader((S/'fence_closeup_pixels.csv').open()):groups[int(row['level'])].append(lightness(im.getpixel((int(row['x']),int(row['y'])))))
snow_values=[lightness(im.getpixel((int(row['x']),int(row['y'])))) for row in csv.DictReader((S/'fence_closeup_snow.csv').open())]
shutil.copy2(S/'fence_closeup_snow.csv',A/'fence_closeup_snow.csv')
painted=[{'samples' :len(g),'median_L':statistics.median(g) if g else None} for g in groups];shutil.copy2(S/'fence_closeup_pixels.csv',A/'fence_closeup_pixels.csv')
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',s,re.I)) for k,s in logs.items() if k!='build'}
close=re.search(r'\[FENCE_CLOSEUP\] distance_m=([\d.]+)',logs['closeup']);runtime=re.search(r'\[STATIC_REMAINING\] root=FM4_Root renderers=(\d+) tris=(\d+)',logs['bench'])
leans=[p['lean'] for p in posts if p['variant']==1]
checks={
 'fresh_logs':all((R/f'logs/fence_refine_{k}.log').stat().st_mtime>(R/'logs/fence_refine_build.log').stat().st_mtime for k in logs if k!='build'),
 'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
 'protected':a['protected_before']==a['protected_after'],
 'share_post':shared and len(posts)==len(spans)+2 and len(post_counts)==len(posts) and all(v==1 for v in post_counts.values()),
 'no_duplicate_columns':min(math.dist(xyz(p['foot']),xyz(q['foot'])) for i,p in enumerate(posts) for q in posts[i+1:])>1.4,
 'column_size':all(all(abs(x-y)<.0001 for x,y in zip(d,[.09,1,.09])) for d in column_dims),
 'embed':all(abs(v-a['embed'])<.001 for v in embed),
 'brief_embed_003':all(abs(v-.03)<.001 for v in embed),
 'gap':max(gap)<=.02,
 'rail_join_inside_post':join_outside==0,
 'snow_columns':len(cap_counts)==len(posts) and all(v==1 for v in cap_counts.values()) and all(all(abs(x-y)<.0001 for x,y in zip(d,[.1,.035,.1])) for d in snow_dims),
 'snow_bevel':bevel_bad==0,
 'snow_rail_size':all(abs(d[0]-.06)<.0001 and abs(d[1]-.02)<.0001 for d in snow_rail_dims),
 'snow_rails':all(len([p for p in s['pieces'] if p['kind']=='rail_snow'])==len([p for p in s['pieces'] if p['kind']=='rail']) for s in spans),
 'snow_color':all(all(abs(p['color'][c]-(((v/255+.055)/1.055)**2.4))<1e-5 for c,v in zip(['r','g','b'],[237,243,248])) and p['color']['a']==0 for p in pieces if p['kind'].endswith('snow')),
 'uv_all_faces':uv_bad==0 and long_bad==0 and axis_bad==0,
 'uv_random':all(0<=s['uv_offset']<=1 for s in spans) and len(set(s['uv_offset'] for s in spans))==len(spans),
 'variant_ratio':counts[0]==146 and all(abs(counts[i]/len(spans)-r)<.015 for i,r in enumerate([.8,.1,.1])),
 'tri_budget':max(s['tris'] for s in spans)<=200,
 'outward_normals':min(volumes)>0,
 'lean':len(lean_actual)==len(leans)>0 and min(lean_actual)>=3.999 and max(lean_actual)<=6.001 and min(outward)>.999,
 'rail_missing':all(len([p for p in s['pieces'] if p['kind']=='rail'])==(1 if s['variant']==2 else 2) for s in spans),
 'crossing':crossings==0,
 'render_geometry':runtime is not None and int(runtime[1])==a['batches']==1 and int(runtime[2])==a['tris']==sum(len(p['triangles'])//3 for p in pieces),
 'snow_visible':len(snow_values)>=10 and statistics.median(snow_values)>=70,
 'wood_visible':all(p['samples']>=10 for p in painted) and painted[2]['median_L']-painted[0]['median_L']>=3,
 'closeup':close is not None and float(close[1])<=3.00001,
 'screenshots':all(v['size']==(1920,1080) and v['fresh'] and v['magenta']==0 for v in shots.values()),
 'walk':'[PLAY]' in logs['walk'] and 'fps_ok=true' in logs['walk'] and 'raycast_misses=0' in logs['walk'],
 'interior':bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True',logs['interior'])),
 'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
 'bad_zero':all(v==0 for v in bad.values()),
 'camera':len(re.findall(r'\[CAM_TEST\].*bad=0',logs['camera']))==2 and 'dist=11.50 h=2.10 look=1.20 fov=48.00' in logs['camera'],
 'outdoor_fps':len([v for v in bench if v['pos'] in ['z01','terrace']])==2 and all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']),
 'terrace_drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),
}
report={'pass':all(checks.values()),'checks':checks,'segments':len(spans),'posts':len(posts),'counts':counts,'tris_per_variant':[[s['tris'] for s in spans if s['variant']==i][0] for i in range(3)],'total_tris':a['tris'],'batches':a['batches'],'embed_min':min(embed),'embed_max':max(embed),'gap_max':max(gap),'spacing_min':min(spacing),'spacing_max':max(spacing),'lean_min':min(lean_actual),'lean_max':max(lean_actual),'snow_columns':len(cap_counts),'snow_PNG_samples':len(snow_values),'snow_PNG_median_L':statistics.median(snow_values) if snow_values else None,'snow_rails':sum(p['kind']=='rail_snow' for p in pieces),'uv_degenerate_triangles':uv_bad,'crossings':crossings,'painted_PNG':painted,'bad':bad,'bench':bench,'shots':shots,'protected_before':a['protected_before'],'protected_after':a['protected_after']}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f"[FENCE_V2] segments={len(spans)} share_post={str(checks['share_post']).lower()} rail_embed={min(embed):.6f}..{max(embed):.6f} gap_max={max(gap):.6f} snow_caps=columns:{len(cap_counts)}+rails:{report['snow_rails']} uv_all_faces={str(checks['uv_all_faces']).lower()}")
print(f"[FENCE_V2] variants=3 counts={counts} lean_deg={min(leans):.6f}..{max(leans):.6f} broken=rail_missing tris={report['tris_per_variant']}")
for k in ['walk','interior','reveal']:print(f'[DEMO] {k} bad={bad[k]}')
for v in bench:print(f"[BENCH] pos={v['pos']} fps_avg={v['fps_avg']:.2f} fps_p1={v['fps_p1']:.2f} drawcall={v['drawcalls']}")
for k,v in checks.items():print(f'[CHECK] {k}={str(bool(v)).lower()}')
print('pass='+str(report['pass']).lower());raise SystemExit(0 if report['pass'] else 1)
