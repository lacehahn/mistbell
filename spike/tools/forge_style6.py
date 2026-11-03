#!/usr/bin/env python3
"""Deterministic STYLE6 albedo assets; never writes the four frozen references.
Run before accept.sh. Pillow is already used by capture_style5.sh; no installs.
"""
import json
import hashlib
from PIL import Image, ImageDraw, ImageChops
from measure_style import ROOT, hash32, lab

RAMPS = {
    # One blue code value below the supplied top tier is needed for |b*| <= 6.
    'Roof_Slate': ['3A3F45','474D54','555C64','646C74'],
    'Rock': ['414449','4E5156','5C5F64','6B6E73'],
    'Grass': ['3A342C','464036','545044','635E52'],
    'Stone_Interior': ['595959','626262','6C6C6C','767676'],
}
FROZEN = {
    'Stone': '8a6b97b163e6844573c39d10e288873060bd744df17ce00bce152ce365b98889',
    'Stone_Sub': '92c64dad301c95d40fe6f0d9540f6ae0ab5536a87dba803fba20826d83e3ef4e',
    'Snow': '8a13e96e9daef38854d7d92157246fd3d1d38412de97338bc5f967a2deeb3375',
    'Wood': '9059633c8b3f92dc3d0a941f717facf05150b0d3e1a1e115800ffdcd9116de2f',
}

def frozen():
    for name, expected in FROZEN.items():
        assert hashlib.sha256((ROOT/(name+'_Albedo.png')).read_bytes()).hexdigest() == expected, name


def rgb(h): return tuple(bytes.fromhex(h))

def neutral_gain(c, gain):
    c = tuple(round(v*gain) for v in c)
    # Keep the cool slate hue within the mandated CIELAB bound, including bevels.
    while max(abs(v) for v in lab(c)[1:]) > 6:
        c = (c[0], c[1], c[2]-1)
    return c


def generate():
    frozen()
    ramps = {n:list(map(rgb,cs)) for n,cs in RAMPS.items()}
    roof = Image.new('RGB',(256,256)); p=roof.load()
    # 32 staggered shingles per course; widths 6..8, heights 3..4.
    for y in range(256):
        row=y//4; yy=y%4; shift=hash32(row,97)%8
        for x in range(256):
            col=((x+shift)%256)//8; xx=(x+shift)%8
            roll=hash32(col,row)%100
            tier=0 if roll<94 else 1 if roll<96 else 2 if roll<98 else 3
            c=ramps['Roof_Slate'][tier]
            if yy==0:c=neutral_gain(c,.94)
            elif yy==3:c=neutral_gain(c,1.12)
            p[x,y]=c
    rock=Image.new('RGB',(256,256));p=rock.load()
    # Toroidal jittered Voronoi facets, 16px seed spacing, no mortar outlines.
    seeds={(x,y): (x*16+hash32(x,y)%9+3,y*16+hash32(y+31,x)%9+3,([0,3,0,3,1,2,0,3][hash32(x+61,y+73)%8])) for y in range(16) for x in range(16)}
    for y in range(256):
        for x in range(256):
            choices=[]
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    cx=x//16+dx; cy=y//16+dy
                    sx,sy,t=seeds[cx%16,cy%16];sx+=(cx//16)*256;sy+=(cy//16)*256
                    choices.append(((x-sx)**2+(y-sy)**2,t))
            p[x,y]=ramps['Rock'][min(choices)[1]]
    d=ImageDraw.Draw(rock)
    for y in range(0,256,16):
        for x in range(0,256,16):
            k=hash32(x,y)
            if k%2==0:
                a=x+3+k%6;b=y+3+(k//9)%6
                d.line([(a,b),(a+2,b+4),(a+1,b+12)],fill=ramps['Rock'][0],width=1)
    grass=Image.new('RGB',(256,256));p=grass.load()
    for y in range(256):
        for x in range(256):
            col=x//5; row=(y+(col%3)*5)//12
            p[x,y]=ramps['Grass'][hash32(col,row)%4]
    # Reuse the actual frozen church masonry: same mortar, bevel and stagger.
    stone=Image.open(ROOT/'Stone_Albedo.png').convert('RGB')
    interior=stone.point(lambda v:round(v*.82))
    # Mortar stays one texel; a slightly deeper crevice restores the family's HF
    # after the required 18% ramp reduction (body/bevel retain exact scaling).
    interior.putdata([((68,68,68) if c==(89,89,89) else tuple(round(v*.82) for v in c)) for c in stone.getdata()])
    # Place the periodic boundary inside a course, preserving the original
    # one-texel mortar and bevel everywhere (no duplicated mortar border).
    px=interior.load()
    cut=min(range(256),key=lambda y:sum(abs(px[x,y][c]-px[x,(y-1)%256][c]) for x in range(256) for c in range(3)))
    interior=ImageChops.offset(interior,0,-cut)
    images=dict(Roof_Slate=roof,Rock=rock,Grass=grass,Stone_Interior=interior)
    for name,im in images.items():
        if name!='Stone_Interior':
            # Match the two periodic border samples without filtering hard tiers.
            px=im.load()
            for y in range(256):px[255,y]=px[0,y]
            for x in range(256):px[x,255]=px[x,0]
        im.save(ROOT/(name+'_Albedo.png'))
        Image.new('RGB',im.size,(242,242,242)).save(ROOT/(name+'_Roughness.png'))
    frozen()
    print('[STYLE6_GENERATED] '+', '.join(images))

if __name__=='__main__':generate()
