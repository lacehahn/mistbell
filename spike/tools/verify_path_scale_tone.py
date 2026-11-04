#!/usr/bin/env python3
"""Task gates recomputed from saved PNGs, source samples, geometry and fresh demos."""
import csv,hashlib,json,math,re,shutil,statistics,colorsys,random
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/path_scale_tone';S=R/'project/Shots'
a=json.loads((A/'assets.json').read_text())
def lab(c):
 v=[x/255 for x in c[:3]];r,g,b=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in v]
 xyz=[(.4124564*r+.3575761*g+.1804375*b)/.95047,.2126729*r+.7151522*g+.072175*b,(.0193339*r+.119192*g+.9503041*b)/1.08883]
 f=[x**(1/3) if x>216/24389 else (24389/27*x+16)/116 for x in xyz]
 return 116*f[1]-16,500*(f[0]-f[1]),200*(f[1]-f[2])
def stats(ps):
 vals=[lab(p) for p in ps]
 return {'samples':len(ps),'L':statistics.mean(v[0] for v in vals),'L_p10':sorted(v[0] for v in vals)[int(len(vals)*.1)],'b':statistics.mean(v[2] for v in vals),'sat':statistics.mean(colorsys.rgb_to_hsv(*(x/255 for x in p))[1] for p in ps)} if ps else {'samples':0,'L':0,'b':0,'sat':0}
pixels={};shots={}
for name in ['cam_play_1080','path_closeup','fence_closeup']:
 p=S/(name+'.png');im=Image.open(p).convert('RGB');shots[name]={'size':im.size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime>(R/'logs/path_scale_tone_build.log').stat().st_mtime};shutil.copy2(p,A/p.name)
 if name!='fence_closeup':
  rows=list(csv.DictReader((S/(name+'_scale_pixels.csv')).open()));pixels[name]={k:stats([im.getpixel((int(v['x']),int(v['y']))) for v in rows if v['kind']==k]) for k in ['path','path_shadow','local_path','local_path_shadow','pilgrim_dark']};shutil.copy2(S/(name+'_scale_pixels.csv'),A/(name+'_scale_pixels.csv'))
 else:
  rows=list(csv.DictReader((S/'fence_closeup_pixels.csv').open()));points=[(int(v['x']),int(v['y'])) for v in rows];pixels[name]=stats([im.getpixel(p) for p in points]);old=Image.open(A/'before_fence_closeup.png').convert('RGB');before=stats([old.getpixel(p) for p in points]);shutil.copy2(S/'fence_closeup_pixels.csv',A/'fence_closeup_pixels.csv')
# Texture-space periodicity: Pearson correlation at the known lattice vectors of BOTH
# rotated layers, at 1, 2 and 3 tiles. Random world points over 32 x 32 metres.
# Reproduce the shader's bilinear, linear-light albedo; no lighting/fog to hide repetition.
tex=Image.open(R/'project/Assets/Art/Textures/Path/path_stone_snow.jpg').convert('RGB');w,h=tex.size;px=tex.load()
def linear(v):
 v/=255;return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def sample(x,y):
 x=(x%1)*w-.5;y=(1-y%1)*h-.5;ix=math.floor(x);iy=math.floor(y);fx=x-ix;fy=y-iy
 return sum(sum(linear(v)*c for v,c in zip(px[(ix+dx)%w,(iy+dy)%h],[.2126729,.7151522,.072175]))*(fx if dx else 1-fx)*(fy if dy else 1-fy) for dx in [0,1] for dy in [0,1])
def tile_offset(x,z):
 h=(x*374761393+z*668265263)&0xffffffff;h=((h^(h>>13))*1274126177)&0xffffffff;h^=h>>16
 return (h&65535)/65536,(h>>16)/65536
def scramble(u,v,x,z):
 ix=math.floor(x);iz=math.floor(z);fx=x-ix;fz=z-iz;fx=fx*fx*(3-2*fx);fz=fz*fz*(3-2*fz)
 total=0
 for dx in [0,1]:
  for dz in [0,1]:
   ox,oz=tile_offset(ix+dx,iz+dz);total+=sample(u+ox,v+oz)*(fx if dx else 1-fx)*(fz if dz else 1-fz)
 return total
def road(x,z):
 x/=a['scale_m'];z/=a['scale_m'];return .68*scramble(.7986355*x-.601815*z,.601815*x+.7986355*z,x,z)+.32*scramble((.7986355*x+.601815*z)*.713+.317,(-.601815*x+.7986355*z)*.713+.613,x+17,z+31)
def corr(x,y):
 mx=statistics.mean(x);my=statistics.mean(y);return sum((a-mx)*(b-my) for a,b in zip(x,y))/math.sqrt(sum((a-mx)**2 for a in x)*sum((b-my)**2 for b in y))
rng=random.Random(135);points=[(rng.uniform(-16,16),rng.uniform(-16,16)) for _ in range(3000)];correlations=[]
for texture_name in ['path_stone_a.jpg','path_stone_snow.jpg']:
 tex=Image.open(R/'project/Assets/Art/Textures/Path'/texture_name).convert('RGB');w,h=tex.size;px=tex.load();base=[road(*p) for p in points]
 for layer,vecs in [('a',[(.7986355,-.601815),(.601815,.7986355)]),('b',[(.7986355/.713,.601815/.713),(-.601815/.713,.7986355/.713)])]:
  for vx,vz in vecs:
   for k in [1,2,3]:correlations.append({'texture':texture_name,'layer':layer,'tiles':k,'shift':[vx*k*a['scale_m'],vz*k*a['scale_m']],'correlation':corr(base,[road(x+vx*k*a['scale_m'],z+vz*k*a['scale_m']) for x,z in points])})
repeat=max(v['correlation'] for v in correlations)
logs={k:(R/f'logs/path_scale_tone_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','fence','walk','interior','reveal','bench']};bench=json.loads((R/'logs/path_scale_tone_bench.json').read_text())['results']
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',v,re.I)) for k,v in logs.items() if k!='build'}
# Independently measure every snow volume in the original generator's local frames.
geometry=json.loads((R/'artifacts/fence_refine/assets.json').read_text());pieces=[p for span in geometry['spans'] for p in span['pieces']]+geometry['terminals'];offset=0;caps=[];rails=[];wood_equal=True
for piece in pieces:
 n=len(piece['vertices']);vs=a['fence_vertices_after'][offset:offset+n];old=a['fence_vertices_before'][offset:offset+n]
 if piece['kind']=='column_snow':caps.append(max(v['y'] for v in vs)-min(v['y'] for v in vs))
 elif piece['kind']=='rail_snow':
  start=piece['start'];end=piece['end'];d={k:end[k]-start[k] for k in ['x','y','z']};length=math.sqrt(sum(v*v for v in d.values()));d={k:v/length for k,v in d.items()};ys=[]
  for v in vs:
   q={k:v[k]-start[k] for k in d};z=(q['x']*d['x']+q['z']*d['z'])/(d['x']**2+d['z']**2);ys.append(q['y']-d['y']*z)
  rails.append(max(ys)-min(ys))
 else:wood_equal &= vs==old
 offset+=n
f=pixels['fence_closeup'];sat_drop=1-f['sat']/before['sat'];l_drop=1-f['L']/before['L'];p=pixels['path_closeup']['path'];dark=pixels['cam_play_1080']['pilgrim_dark'];local=pixels['cam_play_1080']['local_path'];local_L=sum(pixels['cam_play_1080'][k]['L']*pixels['cam_play_1080'][k]['samples'] for k in ['local_path','local_path_shadow'])/sum(pixels['cam_play_1080'][k]['samples'] for k in ['local_path','local_path_shadow']);delta=abs(local_L-dark['L_p10'])
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],'protected':a['protected_before']==a['protected_after'],'wood_geometry':wood_equal and a['uv_unchanged'],'scale':abs(a['scale_m']-1.35)<1e-5,'repeat':repeat<=.35,'path_gain':1.05-1e-6<=a['path_gain']<=1.08+1e-6,'path_L':p['samples']>=100 and 58<=p['L']<=62,'fence_b':f['samples']>=100 and f['b']<=4,'fence_sat_drop':.15<=sat_drop<=.20,'fence_L_drop':.04<=l_drop<=.06,'cap':len(caps)==183 and all(abs(v-.05)<.0001 for v in caps),'rail_snow':len(rails)==345 and all(abs(v-.03)<.0001 for v in rails),'pilgrim_multiplier':abs(a['pilgrim_after']['r']-1.22)<1e-5,'contrast':dark['samples']>=10 and local['samples']>=10 and delta>=8,'demos':all(bad[k]==0 for k in ['walk','interior','reveal']) and '[REVEAL] gate=pass' in logs['reveal'] and '[INT_PASS]' in logs['interior'] and 'fps_ok=true' in logs['walk'],'bad_zero':all(v==0 for v in bad.values()),'fps':all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']),'drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),'shots':all(v['size']==(1920,1080) and v['fresh'] for v in shots.values())}
report={'pass':all(checks.values()),'checks':checks,'pixels':pixels,'fence_before':before,'sat_drop':sat_drop,'L_drop':l_drop,'cap_height_range':[min(caps),max(caps)],'rail_snow_range':[min(rails),max(rails)],'repeat_score':repeat,'repeat_correlations':correlations,'road_local_all_L':local_L,'path_all_L':sum(pixels['path_closeup'][k]['L']*pixels['path_closeup'][k]['samples'] for k in ['path','path_shadow'])/sum(pixels['path_closeup'][k]['samples'] for k in ['path','path_shadow']),'delta_L':delta,'bad':bad,'bench':bench,'shots':shots}
report['metric_notes']={'path':'CIELAB D65 mean of geometry-masked unshadowed near paving; shadow/all-surface values are separately disclosed. Shadow mask uses physics toward the authored sun.','contrast':'Gameplay PNG dark source palette (luminance <=55/255), opaque atlas mask eroded by one texel; rendered L* 10th percentile against weighted mean local road including shadow, radius 1.6m.','fence':'Same coordinates in before/after no-postprocessing close-up, sunlit wood only; mean per-pixel HSV saturation relative drop and mean CIELAB lightness drop. Tint includes a cool hue correction toward b*=-2.','repeat':'CPU reference of shader linear-light bilinear albedo over 3000 seeded 32x32m points, both texture endpoints; maximum Pearson autocorrelation at both rotated lattice axes, 1/2/3 tiles. Not a screenshot-derived repetition score.'}
report['source_sha256']={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [R/'project/Assets/Art/Shaders/PathTone.shader',R/'project/Assets/Art/Textures/Path/path_stone_a.jpg',R/'project/Assets/Art/Textures/Path/path_stone_snow.jpg']}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f"[PATH_TEX2] scale_m={a['scale_m']} repeat_score={repeat:.6f} luma={p['L']:.6f}")
print(f"[FENCE_COLOR] b_star={f['b']:.6f} sat_drop={sat_drop:.6f} L_drop={l_drop:.6f} cap_h={a['cap_h']} rail_snow={a['rail_snow']}")
print(f"[PILGRIM_CONTRAST] delta_L={delta:.6f} dark_L_p10={dark['L_p10']:.6f} road_L={local_L:.6f}")
for k in ['walk','interior','reveal']:print(f'[DEMO] {k} bad={bad[k]}')
for v in bench:print(f"[BENCH] {v['pos']} fps={v['fps_avg']:.2f} drawcall={v['drawcalls']}")
for k,v in checks.items():print(f'[CHECK] {k}={str(v).lower()}')
print('pass='+str(report['pass']).lower());raise SystemExit(0 if report['pass'] else 1)
