#!/usr/bin/env python3
"""Phase-one material measurement. Standard library + macOS sips; no pip packages.

step_ratio: fraction of pixels touching a different *ramp level* in the 4-neighbourhood,
excluding differences between stone block brightness variants. Edge comparison wraps.
seam_grad: maximum normalized RGB difference between paired opposite border texels.
hf: identical horizontal two-pixel RGB absolute-difference formula as measure_shot.py,
measured within each explicit ROI, over its whole area (no silent half-ROI crop).
A three-sample result never claims the deferred seven-material/four-element full gate.
Exit status validates texture checks only; screenshot HF and the deferred full gate
are printed separately and never inferred from that exit status.
"""
import argparse
import json
import pathlib
import struct
import subprocess
import sys
import zlib

REPO = pathlib.Path(__file__).resolve().parents[2]
PROJECT = REPO / 'spike/project'
ROOT = PROJECT / 'Assets/Art/MaterialForge'
RANGES = {'Snow': (16, 24), 'Stone': (24, 32), 'Wood': (20, 28)}


def load(path):
    data = pathlib.Path(path).read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('PNG required')
    pos, compressed = 8, bytearray()
    while pos < len(data):
        size = struct.unpack('>I', data[pos:pos+4])[0]
        tag, chunk = data[pos+4:pos+8], data[pos+8:pos+8+size]
        if tag == b'IHDR':
            w, h, depth, mode, _, _, interlace = struct.unpack('>IIBBBBB', chunk)
        elif tag == b'IDAT':
            compressed.extend(chunk)
        pos += size+12
    if depth != 8 or mode not in (2, 6) or interlace:
        raise ValueError('Need non-interlaced 8-bit RGB/RGBA PNG')
    bpp = 4 if mode == 6 else 3
    stride, stream, previous, result = w*bpp, zlib.decompress(compressed), bytearray(w*bpp), bytearray()
    for y in range(h):
        start = y*(stride+1)
        filt, row = stream[start], bytearray(stream[start+1:start+1+stride])
        for i in range(stride):
            a, b, c = row[i-bpp] if i >= bpp else 0, previous[i], previous[i-bpp] if i >= bpp else 0
            if filt == 1: pred = a
            elif filt == 2: pred = b
            elif filt == 3: pred = (a+b)//2
            elif filt == 4:
                p = a+b-c
                pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
                pred = a if pa <= pb and pa <= pc else b if pb <= pc else c
            elif filt == 0: pred = 0
            else: raise ValueError('Invalid PNG filter')
            row[i] = (row[i]+pred) & 255
        for x in range(w): result.extend(row[x*bpp:x*bpp+3])
        previous = row
    return result, w, h


def png(path, rgb, w, h):
    def chunk(tag, data):
        return struct.pack('>I', len(data))+tag+data+struct.pack('>I', zlib.crc32(tag+data)&0xffffffff)
    raw = b''.join(b'\0'+rgb[y*w*3:(y+1)*w*3] for y in range(h))
    pathlib.Path(path).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR', struct.pack('>IIBBBBB', w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))


def texture(entry):
    buf, w, h = load(PROJECT / entry['albedo'])
    palette = sorted(set(tuple(buf[i:i+3]) for i in range(0,len(buf),3)), key=sum)
    # Closest known level in any of the block brightness variants; avoids counting
    # ±8% block variation as a fourth lighting level.
    base = {'Snow': ['DCE8F2','EDF3F8','F6FAFD'], 'Stone': ['8C7C66','A8967C','C2B49A'], 'Wood': ['4A3426','6B4A32','8A6D52']}[entry['name']]
    candidates=[]
    for level, value in enumerate(base):
        for gain in ([.92,1,1.08] if entry['name']=='Stone' else [1]):
            candidates.append((tuple(round(int(value[i:i+2],16)*gain) for i in (0,2,4)),level))
    levels = {c: min(candidates,key=lambda q:sum((a-b)**2 for a,b in zip(c,q[0])))[1] for c in palette}
    pixels = [tuple(buf[i:i+3]) for i in range(0,len(buf),3)]
    boundary = sum(any(levels[pixels[y*w+x]] != levels[pixels[yy*w+xx]] for xx,yy in (((x+1)%w,y),((x-1)%w,y),(x,(y+1)%h),(x,(y-1)%h))) for y in range(h) for x in range(w))
    pairs = [(pixels[y*w],pixels[y*w+w-1]) for y in range(h)] + [(pixels[x],pixels[(h-1)*w+x]) for x in range(w)]
    seam = max(abs(a-b)/255 for p,q in pairs for a,b in zip(p,q))
    scale = entry['tiling_x']
    # Check the actual serialized Unity material, not just the generated manifest.
    mat=(PROJECT / entry['material']).read_text()
    import re
    section=re.search(r'- _BaseMap:\s*\n\s*m_Texture:.*?\n\s*m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat)
    if not section: raise ValueError('Cannot read material BaseMap tiling: '+entry['material'])
    sx,sy=map(float,section.groups())
    if abs(sx-scale)>1e-6 or abs(sy-entry['tiling_y'])>1e-6: raise ValueError('Stale manifest tiling')
    density=w*sx
    low,high=RANGES[entry['name']]
    rough,rw,rh=load(PROJECT / entry['roughness'])
    importer=(PROJECT / (entry['albedo']+'.meta')).read_text()
    rough_importer=(PROJECT / (entry['roughness']+'.meta')).read_text()
    point=all('filterMode: 0' in m and 'enableMipMap: 0' in m for m in (importer,rough_importer))
    if not re.search(r'- _ForgeSurface: '+str(entry['forge_surface'])+r'\b',mat):
        raise ValueError('Forge projection is not enabled on material')
    result=dict(palette=len(palette),texel_per_m=density,step_ratio=boundary/(w*h),seam_grad=seam,point_no_mipmap=point,roughness_palette=len(set(tuple(rough[i:i+3]) for i in range(0,len(rough),3))))
    result['pass']=len(palette)<=12 and low<=density<=high and .08<=result['step_ratio']<=.25 and seam<=.02 and point and (w,h,rw,rh)==(256,256,256,256)
    return result


def hf(buf,w,h,box):
    x0,y0,x1,y1=box
    if not 0<=x0<x1<=w or not 0<=y0<y1<=h: raise ValueError('ROI outside image')
    total=n=0
    for y in range(y0,y1,2):
        for x in range(x0,x1-2,2):
            i=(y*w+x)*3
            total+=sum(abs(buf[i+c]-buf[i+6+c]) for c in range(3)); n+=3
    return total/max(n,1)/255


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--shot',type=pathlib.Path,default=PROJECT/'Shots/art_close.png')
    ap.add_argument('--regions',type=pathlib.Path,default=REPO/'spike/editor/material_style_regions.json')
    args=ap.parse_args()
    manifest=json.loads((ROOT/'manifest.json').read_text())
    results={e['name']:texture(e) for e in manifest['materials']}
    for name,r in results.items():
        print(f"[STYLE] {name} palette={r['palette']} texel_per_m={r['texel_per_m']:.4f} step_ratio={r['step_ratio']:.4f} seam_grad={r['seam_grad']:.4f} point_no_mipmap={r['point_no_mipmap']} pass={r['pass']}")
    # Required sips downsample; enlargement is explicitly nearest neighbour using stdlib.
    small=args.shot.parent/'style_480.png'
    subprocess.run(['sips','-z','270','480',str(args.shot),'--out',str(small)],check=True,stdout=subprocess.DEVNULL)
    sb,sw,sh=load(small)
    up=bytearray()
    for y in range(sh):
        row=b''.join(bytes(sb[(y*sw+x)*3:(y*sw+x)*3+3])*4 for x in range(sw))
        for _ in range(4): up.extend(row)
    png(args.shot.parent/'style_480_upscaled.png',up,sw*4,sh*4)
    spreads=[]; energies={}
    if args.regions.exists():
        regions=json.loads(args.regions.read_text())
        for label,path in [('native',args.shot),('480',small)]:
            buf,w,h=load(path)
            values={name:hf(buf,w,h,[round(v*(w if i%2==0 else h)) for i,v in enumerate(box)]) for name,box in regions['normalized_rects'].items()}
            # Symmetric two-way spread: (max-min)/min; zero energy is not a pass.
            spread=(max(values.values())-min(values.values()))/min(values.values()) if values and min(values.values())>0 else float('inf')
            spreads.append(spread); energies[label]=values
            print('[STYLE_HF] resolution='+label+' '+ ' '.join(f'{k}={v:.6f}' for k,v in values.items())+f' spread={spread:.6f}')
    spread=max(spreads) if spreads else float('inf')
    palette_ok=all(r['palette']<=12 for r in results.values())
    texel_ok=all(RANGES[n][0]<=r['texel_per_m']<=RANGES[n][1] for n,r in results.items())
    sample_ok=all(r['pass'] for r in results.values())
    report=dict(scope=manifest['phase'],materials=results,hf=energies,hf_spread=spread,sample_texture_pass=sample_ok,full_gate_pass=False,deferred=['Roof_Slate','Rock','Grass','InteriorStone','four-element HF','48-main-color frame gate'])
    (ROOT/'measurement.json').write_text(json.dumps(report,indent=2)+'\n')
    print(f'[STYLE_SAMPLE_GATE] textures_ok={sample_ok} hf_ok={spread<=.20} pending_visual_confirmation=True')
    print(f'[STYLE_GATE] palette_ok={palette_ok} texel_ok={texel_ok} hf_spread={spread:.6f} pass=False scope=phase1_incomplete')
    return 0 if sample_ok else 1

if __name__=='__main__': sys.exit(main())
