"""Measure delivered PNG pixels using the first-visible-surface geometry mask."""
import json,re,hashlib
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1]
frame=Image.open(R/'project/Shots/interior_inside.png').convert('RGB')
mask=Image.open(R/'project/Shots/interior_tone_inside_mask.png').convert('RGB')
colors={'left_wall':(255,255,0),'right_wall':(255,0,255),'ceiling':(0,0,255),'altar_wall':(0,255,0)}
samples={n:[] for n in colors}
def lab(rgb):
    c=[v/255 for v in rgb];r,g,b=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c]
    xyz=[(.4124564*r+.3575761*g+.1804375*b)/.95047,.2126729*r+.7151522*g+.072175*b,(.0193339*r+.119192*g+.9503041*b)/1.08883]
    x,y,z=[v**(1/3) if v>216/24389 else v*24389/3132+16/116 for v in xyz]
    return 116*y-16,500*(x-y),200*(y-z)
for y in range(mask.height):
    for x in range(mask.width):
        color=mask.getpixel((x,y))
        for name,key in colors.items():
            if name=='altar_wall' and not (.48<=x/mask.width<=.52 and .47<=y/mask.height<=.59):continue
            if color==key:samples[name].append(lab(frame.getpixel((x*4+2,y*4+1))))
measure={n:dict(pixels=len(v),**{k:sum(row[j] for row in v)/len(v) if v else 0 for j,k in enumerate(['L','a','b'])}) for n,v in samples.items()}
# The fixed nave view contains no visible ceiling; measure it from the existing lookup view.
lookup=Image.open(R/'project/Shots/interior_lookup.png').convert('RGB')
lmask=Image.open(R/'project/Shots/interior_tone_lookup_mask.png').convert('RGB')
v=[lab(lookup.getpixel((x*4+2,y*4+1))) for y in range(lmask.height) for x in range(lmask.width) if lmask.getpixel((x,y))==(0,0,255)]
measure['ceiling']=dict(pixels=len(v),**{k:sum(row[j] for row in v)/len(v) if v else 0 for j,k in enumerate(['L','a','b'])})
w=sorted([measure['left_wall'],measure['right_wall']],key=lambda v:v['L']);shadow,lit=w
ceiling=measure['ceiling'];altar=measure['altar_wall']
print('[WALL_TONE2] b_star=%.3f a_star=%.3f lit_L=%.3f shadow_L=%.3f split=%.3f metric=PNG_CIELAB_D65'%(sum(v['b'] for v in w)/2,sum(v['a'] for v in w)/2,lit['L'],shadow['L'],lit['L']-shadow['L']))
print('[LAYER] ceiling_L=%.3f altar_L=%.3f wall_lit_L=%.3f'%(ceiling['L'],altar['L'],lit['L']))
checks={'wall_lit':36<=lit['L']<=42,'wall_shadow':20<=shadow['L']<=24,'split':lit['L']-shadow['L']>=12,'wall_chroma':all(8<=v['b']<=11 and 2<=v['a']<=5 for v in w),'ceiling':ceiling['L']<=22 and 6<=ceiling['b']<=9,'layer':lit['L']-ceiling['L']>=14 and altar['L']>=ceiling['L']+6,'altar':28<=altar['L']<=34}
build=(R/'logs/interior_tone_build.log').read_text();entry=(R/'logs/interior_tone_interior.log').read_text()
digest=json.loads((R/'artifacts/interior_tone/protected_digests.json').read_text())
checks['protected_digest']=digest['before']==digest['after']
for tag in ['CHANDELIER_STEM','WALL_PAINTING']:
    print(next(line for line in build.splitlines() if line.startswith('['+tag+']')))
checks.update(build='[PERF_BUILD] result=Succeeded errors=0' in build,protected='[TONE_PROTECTED]' in build,entry='entry gates failed' not in entry and bool(re.search(r'\[INT_PASS\] light=True domeLight=True ceilingSeen=True domeSeen=True .*visible=True obstacles=True walked=True',entry)),stem='[CHANDELIER_STEM] beam_width=0 removed=true stem_width=0.030' in build,painting='[WALL_PAINTING] removed=true' in build)
benchfile=R/'logs/interior_tone_bench.json'
bench=json.loads(benchfile.read_text()) if benchfile.exists() else {}
interior=next((v for v in bench.get('results',[]) if v['pos']=='interior'),{})
checks['fresh_benchmark']=benchfile.exists() and benchfile.stat().st_mtime>(R/'logs/interior_tone_build.log').stat().st_mtime
checks['runtime_errors']=not re.search(r'(Exception:|Shader error|entry gates failed)',entry)
checks['performance']=0<interior.get('drawcalls',999)<=300 and interior.get('fps_avg',0)>=200
shots={}
for n in ['interior_inside','windows_closeup','interior_lookup']:
    p=R/f'project/Shots/{n}.png';shots[n]={'size':Image.open(p).size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime>(R/'logs/interior_tone_build.log').stat().st_mtime}
checks['screenshots']=all(v['size']==(1920,1080) and v['fresh'] for v in shots.values())
shader=(R/'project/Assets/Art/Shaders/InteriorWallBlocks.shader').read_text()
checks['block_layout']=all(t in shader for t in ['0.88 + hash * 0.24','0.1 + 0.2 *','band*6.0 + 3.0','start+1.5','lerp(0.80,1.0','0.02-aa,0.02+aa'])
checks['lamps_chains']=bool(re.search(r'\[INT_PASS\].*lamps=20 shafts=3',entry)) and 'links=3' in build
report=dict(accepted=all(checks.values()),checks=checks,measurements=measure,interior=interior,shots=shots)
(R/'artifacts/interior_tone/measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2));raise SystemExit(0 if report['accepted'] else 1)
