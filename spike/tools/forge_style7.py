#!/usr/bin/env python3
"""V7 palette-only remap. Preserve masonry/shingle masks and all five frozen PNGs."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops
from measure_style import ROOT

FROZEN = json.loads(Path(__file__).with_name('style7_frozen.json').read_text())
STONE = [(105+12*i,91+12*i,66+12*i) for i in range(4)]
INTERIOR = [(79+12*i,70+12*i,55+12*i) for i in range(4)]
ROOF = [(70,75,81),(83,89,96),(97,104,112),(112,120,128)]

def gain(c,g): return tuple(round(v*g) for v in c)
def stone_palette(ramp, indoor=False):
    mortar=(58,49,36) if indoor else gain(ramp[0],.82)
    return [mortar]+ramp+[gain(c,1.12) for c in ramp]
def roof_palette(ramp, bevel=1.08):
    return ramp+[gain(c,.94) for c in ramp]+[gain(c,bevel) for c in ramp]
def frozen():
    same={n:hashlib.sha256((ROOT/(n+'_Albedo.png')).read_bytes()).hexdigest()==s for n,s in FROZEN.items()}
    assert all(same.values()),same
    return same

def remap(name, old, new):
    path=ROOT/(name+'_Albedo.png')
    im=Image.open(path).convert('RGB'); pixels=list(im.getdata())
    # Idempotence: a second acceptance run does not stack gains or re-encode PNGs.
    if set(pixels)<=set(new): return im
    assert set(pixels)<=set(old),(name,set(pixels)-set(old))
    mapping=dict(zip(old,new)); im.putdata([mapping[p] for p in pixels]); im.save(path)
    return im

def generate():
    frozen()
    stone=remap('Stone',[(89,89,89)]+[(v,v,v) for v in (108,120,132,144)]+[(v,v,v) for v in (121,134,148,161)],stone_palette(STONE))
    old_roof=[(58,63,69),(71,77,84),(85,92,100),(100,108,116)]
    # V6 cool-bound clipping subtracts one blue code value on the brightest bevel.
    old=roof_palette(old_roof,1.12); old[-2]=(95,103,111); old[-1]=(112,121,129)
    remap('Roof_Slate',old,roof_palette(ROOF))
    mapping=dict(zip(stone_palette(STONE),stone_palette(INTERIOR,True)))
    interior=stone.copy(); interior.putdata([mapping[p] for p in stone.getdata()])
    px=interior.load(); w,h=interior.size
    cut=min(range(h),key=lambda y:sum(abs(px[x,y][c]-px[x,(y-1)%h][c]) for x in range(w) for c in range(3)))
    ImageChops.offset(interior,0,-cut).save(ROOT/'Stone_Interior_Albedo.png')
    frozen()
    print('[STYLE7_GENERATED] Stone Roof_Slate Stone_Interior; five frozen SHA verified')

if __name__=='__main__':generate()
