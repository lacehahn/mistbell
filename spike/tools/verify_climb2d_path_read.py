#!/usr/bin/env python3
"""Verify this task against captured pixels and the existing flow/colour gates."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'spike/artifacts/climb2d'
REPORT=ROOT/'spike/artifacts/path_read'
LOG=(ROOT/'spike/logs/climb2d_run.log').read_text()

def need(ok,msg):
    if not ok: raise SystemExit('FAIL: '+msg)

def lab(rgb):
    r,g,b=[v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in rgb[:3]]
    f=lambda v:v**(1/3) if v>.008856 else 7.787*v+16/116
    fy=f(.2126729*r+.7151522*g+.072175*b)
    return 116*fy-16,200*(fy-f((.0193339*r+.119192*g+.9503041*b)/1.08883))

subprocess.run([sys.executable,str(ROOT/'spike/tools/verify_climb2d_grade.py')],check=True)
protected=json.loads((REPORT/'protected.json').read_text())
for name,digest in protected.items():
    need(hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,'protected '+name)
edge=Image.open(OUT/'path_read_edge.png').convert('RGBA')
alpha=set(edge.getchannel('A').getdata())-{0}
need(alpha=={31,69,107},'12px band alpha .42/.27/.12: '+str(alpha))
source=(ROOT/'spike/project/Assets/Scripts/Climb2D.cs').read_text()
need('[PATH_EDGE] width=12 alpha=0.42→0.12 closed=false' in LOG,'open edge runtime gate')
need('StoneDetail=.35f' in source and '*StoneDetail)*.9f' in source,'detail weight and brightness multiplier')
base=json.loads((REPORT/'baseline.json').read_text())['commit']
before=subprocess.check_output(['git','show',base+':spike/project/Assets/Scripts/Climb2D.cs'],cwd=ROOT).decode()
def method(text,signature):
    start=text.index(signature);brace=text.index('{',start);depth=1;end=brace+1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
for signature in ('Color32 Grade(', 'void ApplyGrade(', 'void DrawTrees(', 'void BuildGradeLayers(', 'void BuildDepthStyle(', 'void BuildStepStyle('):
    need(method(before,signature)==method(source,signature),'unchanged '+signature)

# Independently locate the downhill boundary of every attached rock column.
surface=Image.open(OUT/'path_read_surface.png').convert('RGBA')
route=json.loads((ROOT/'spike/project/Assets/Resources/Climb2DArt/route.json').read_text())
expected_columns=[]
for point,width in zip((4,7,10),(50,75,100)):
    centre=round(route['points'][point]['x']*route['worldScaleX'])
    near=round((route['sourceHeight']-route['points'][point]['y'])*route['worldScaleY'])
    count=0
    for x in range(centre-width//2,centre-width//2+width):
        edges=[y for y in range(surface.height) if surface.getpixel((x,surface.height-1-y))[3]>=128 and (y==0 or surface.getpixel((x,surface.height-y))[3]<128)]
        need(edges,'downhill edge exists')
        edge_y=min(edges,key=lambda y:abs(y-near))
        # Both rows of the 8px bite must actually fall inside opaque road.
        need(all(surface.getpixel((x,surface.height-1-y))[3]>=128 for y in (edge_y,edge_y+1)),'8px opaque overlap')
        count+=1
    expected_columns.append(count)

results=[]
for name in ('ramp_mid','turn'):
    shot=Image.open(OUT/f'climb2d_{name}.png').convert('RGB')
    mask=Image.open(OUT/f'contrast_mask_{name}.png').convert('RGB')
    need(shot.size==(1920,1080),'screenshot dimensions')
    road=[];ground=[]
    for y in range(270):
        for x in range(480):
            mark=mask.getpixel((x,y))
            if mark==(255,0,0):road.append(lab(shot.getpixel((x*4+2,y*4+2)))[0])
            elif mark==(0,255,0):ground.append(lab(shot.getpixel((x*4+2,y*4+2)))[0])
    path_l=sum(road)/len(road);ground_l=sum(ground)/len(ground);delta=ground_l-path_l
    need(delta>=10,'screenshot delta L* >=10')
    line=next(l for l in LOG.splitlines() if l.startswith('[PATH_READ]') and f'shot={name} ' in l)
    need(abs(float(re.search(r'path_L=([\d.]+)',line)[1])-path_l)<.01,'screenshot/path log agrees')
    bite=next(l for l in LOG.splitlines() if l.startswith('[BITE]') and f'shot={name}' in l)
    need('overlaps=3 shadow=3px overlap_px=8 alpha=0.3 raster=display' in bite,'three bites gate')
    columns=list(map(int,re.search(r'columns=([\d,]+)',bite)[1].split(',')))
    need(columns==expected_columns,'all three rock caps attached across their widths')
    contact=Image.open(OUT/f'path_contact_{name}.png').convert('RGBA')
    samples=0;errors=0;fourth_diff=0
    for y in range(270):
        for x in range(480):
            c=contact.getpixel((x,y))
            # Character has priority over contact strips at the fixed player anchor.
            if not c[3] or (abs(x-240)<19 and 93<=y<=136):continue
            expected=c[:3]
            for dy in range(3):
                actual=shot.getpixel((x*4+2,y*4+dy))
                errors+=max(abs(a-b) for a,b in zip(expected,actual))>1
            fourth_diff+=shot.getpixel((x*4+2,y*4+3))!=expected
            samples+=1
    need(samples>10 and errors==0 and fourth_diff>samples*.8,'actual 3px contact strips '+str((samples,errors,fourth_diff)))
    result=dict(shot=name,path_L=path_l,ground_L=ground_l,delta=delta,stone_detail=.35,edge_width_px=12,edge_alpha=[.42,.12],closed=False,overlaps=3,overlap_px=8,columns=columns,shadow_px=3,contact_samples=samples,contact_errors=errors)
    results.append(result)
    print(f'[PATH_READ] shot={name} path_L={path_l:.3f} ground_L={ground_l:.3f} delta={delta:.3f} stone_detail=0.35')
    print(f'[BITE] overlaps=3 overlap=8px shadow=3px columns={columns} contact_samples={samples} pixel_errors={errors}')
report=dict(screenshots=results,protected_files=len(protected),flow=json.loads((OUT/'grade_acceptance.json').read_text())['timing'])
(REPORT/'acceptance.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('[PATH_EDGE] width=12 alpha=0.42→0.12 closed=false')
print(f'ACCEPT climb2d_path_read protected={len(protected)} bad=0')
