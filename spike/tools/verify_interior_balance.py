"""Recompute fixed-ROI and geometry-labelled image measurements; fail any task gate."""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image, ImageStat

ROOT=Path(__file__).resolve().parents[1]
log=(ROOT/'logs/interior_balance_interior.log').read_text()
build=(ROOT/'logs/interior_balance_build.log').read_text()
def line(tag):
    return next(x for x in log.splitlines() if x.startswith(f'[{tag}]'))
def linear(a):
    return a/12.92 if a<=.04045 else ((a+.055)/1.055)**2.4
def luma(rgb,lin=False):
    c=[v/255 for v in rgb]
    if lin:c=list(map(linear,c))
    return sum(a*b for a,b in zip(c,(.2126,.7152,.0722)))
inside=Image.open(ROOT/'project/Shots/interior_inside.png').convert('RGB')
w,h=inside.size
def roi(box):
    x0,y0,x1,y1=box
    c=ImageStat.Stat(inside.crop((int(x0*w),int(y0*h),int(x1*w),int(y1*h)))).mean
    return luma(c,True)
l=roi((.06,.35,.20,.62));r=roi((.80,.35,.94,.62));n=roi((.36,.72,.64,.92))
lookup=Image.open(ROOT/'project/Shots/interior_lookup.png').convert('RGB')
mask=Image.open(ROOT/'project/Shots/interior_dome_mask.png').convert('RGB')
values={}
for name,color in [('rib',(255,0,0)),('plaster',(0,255,0)),('oculus',(0,0,255))]:
    samples=[]
    for y in range(270):
        for x in range(480):
            if mask.getpixel((x,y))==color:
                # Match bottom-left C# sampling after converting to PNG coordinates.
                sx=int((x+.5)*w/480);sy=h-1-int((269-y+.5)*h/270)
                samples.append(luma(lookup.getpixel((sx,sy))))
    values[name]={'pixels':len(samples),'display_luma':sum(samples)/len(samples) if samples else 0}
contrast=abs(values['rib']['display_luma']-values['plaster']['display_luma'])/max(1e-9,values['plaster']['display_luma'])
pairs=[list(map(int,p.split(','))) for p in line('PEW_SAMPLES').split('] ',1)[1].split(';')]
edge=max(abs(luma(inside.getpixel((x,h-1-y)),True)-luma(inside.getpixel((u,h-1-v)),True)) for x,y,u,v in pairs)
benchpath=ROOT/'logs/interior_balance_bench.json'
bench=json.loads(benchpath.read_text()) if benchpath.exists() else {'results':[]}
interior=next((x for x in bench['results'] if x['pos']=='interior'),{})
checks={
 'balance':l<=.09 and r>=.045 and l/r<=2 and .030<=n<=.050,
 'dome':'plaster=true ribs_visible=true' in line('DOME_SKIN') and contrast>=.10 and values['oculus']['display_luma']>=.55,
 'ground':'shadows=12' in line('PEW_GROUND') and len(pairs)==48 and edge<=.02,
 'demo':bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True',log)) and 'entry gates failed' not in log,
 'build':'[PERF_BUILD] result=Succeeded errors=0' in build,
 'protected':'[INT_BALANCE_INVARIANTS] floor_materials_identical=true colliders_identical=true idempotent=true' in build,
 'protected_art':'[INT_BALANCE_PROTECTED] exterior_camera_altar_royal_door_identical=true' in build,
 'fresh_benchmark':benchpath.exists() and benchpath.stat().st_mtime>(ROOT/'logs/interior_balance_build.log').stat().st_mtime,
 'budget':interior.get('drawcalls',9999)<=280 and interior.get('fps_avg',0)>=200,
 'resolution':(w,h)==(1920,1080),
 'fresh_shots':all((ROOT/f'project/Shots/{name}.png').stat().st_mtime>(ROOT/'logs/interior_balance_build.log').stat().st_mtime for name in ['interior_inside','interior_lookup','interior_dome_mask']),
}
shots={}
for name in ['interior_inside','interior_lookup','interior_dome_mask']:
    p=ROOT/f'project/Shots/{name}.png'
    shots[name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'size':Image.open(p).size}
report={'accepted':all(checks.values()),'checks':checks,'balance':{'left':l,'right':r,'nave':n,'ratio':l/r},'dome':values,'rib_contrast':contrast,'pew_edge_delta':edge,'pew_pairs':pairs,'interior':interior,'shots':shots,'runtime_logs':[line(t) for t in ['INT_BALANCE','DOME_SKIN','PEW_GROUND','INT_PASS']]}
out=ROOT/'artifacts/interior_balance/measurements.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('pew_pairs','shots')},ensure_ascii=False,indent=2))
raise SystemExit(0 if report['accepted'] else 1)
